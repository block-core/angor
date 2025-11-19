using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Services;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class Investments
{
    public record InvestmentsPortfolioRequest(WalletId WalletId) : IRequest<Result<IEnumerable<InvestedProjectDto>>>;
    
    public class InvestmentsPortfolioHandler(
        IAngorIndexerService angorIndexerService,
        IPortfolioService investmentService,
        IProjectService projectService,
        IInvestmentConversationService conversationService,
        INetworkConfiguration networkConfiguration,
        ILogger<InvestmentsPortfolioHandler> logger
    ) : IRequestHandler<InvestmentsPortfolioRequest,Result<IEnumerable<InvestedProjectDto>>>
    {

        public async Task<Result<IEnumerable<InvestedProjectDto>>> Handle(InvestmentsPortfolioRequest request, CancellationToken cancellationToken)
        {
            var investmentRecordsLookup = await investmentService.GetByWalletId(request.WalletId.Value);

            if (investmentRecordsLookup.IsFailure)
                return Result.Failure<IEnumerable<InvestedProjectDto>>("Failed to retrieve investment records: " + investmentRecordsLookup.Error);
            
            if (investmentRecordsLookup.Value.ProjectIdentifiers.Count == 0)
                return Result.Success(Enumerable.Empty<InvestedProjectDto>());

            var ids = investmentRecordsLookup.Value.ProjectIdentifiers
                .Select(id => new ProjectId(id.ProjectIdentifier)).ToArray();

            var lookup = await projectService.GetAllAsync(ids);
            
            if (lookup.IsFailure)
                return Result.Failure<IEnumerable<InvestedProjectDto>>("Failed to retrieve projects: " + lookup.Error);

            var investmentLookupTasks = lookup.Value.ToList().Select(async project =>
            {
                try
                {
                    var investmentRecord = investmentRecordsLookup.Value.ProjectIdentifiers
                        .First(x => x.ProjectIdentifier == project.Id.Value);

                    var investmentTask = Result.Try(() => angorIndexerService.GetInvestmentAsync(project.Id.Value, investmentRecord.InvestorPubKey));
                    var statsTask = Result.Try(() => 
                        angorIndexerService.GetProjectStatsAsync(project.Id.Value));

                    await Task.WhenAll(investmentTask, statsTask);

                    var investment = investmentTask.Result.IsSuccess ? investmentTask.Result.Value : null;
                    var stats = statsTask.Result.IsSuccess ? statsTask.Result.Value : (project.Id.Value, null);

                    var dto = new InvestedProjectDto
                    {
                        Id = project.Id.Value,
                        Name = project.Name,
                        Description = project.ShortDescription,
                        LogoUri = project.Picture,
                        Target = new Amount(project.TargetAmount),
                        FounderStatus = investment == null ? FounderStatus.Requested : FounderStatus.Approved,
                        InvestmentStatus = investment == null ? InvestmentStatus.Invalid : InvestmentStatus.Invested,
                        Investment = new Amount(investment?.TotalAmount ?? 0),
                        InvestmentId = investment?.TransactionId ?? string.Empty,
                        Raised = new Amount(stats.stats?.AmountInvested ?? 0),
                        InRecovery = new Amount(stats.stats?.AmountInPenalties ?? 0)
                    };

                    if (investment != null)
                        return Result.Success(dto);

                    // Sync conversations from Nostr for this project
                    var syncResult = await conversationService.SyncConversationsFromNostrAsync(
                        request.WalletId,
                        project.Id,
                        project.NostrPubKey);

                    if (syncResult.IsFailure)
                    {
                        logger.LogWarning("Failed to sync conversations for project {ProjectId}: {Error}", 
                            project.Id.Value, syncResult.Error);
                    }

                    // Get the conversation for this specific request event
                    Result<InvestmentConversation?> conversationResult;
                    if (!string.IsNullOrEmpty(investmentRecord.RequestEventId))
                    {
                        conversationResult = await conversationService.GetConversationByRequestEventIdAsync(
                            request.WalletId,
                            project.Id,
                            investmentRecord.RequestEventId);
                    }
                    else
                    {
                        // If we don't have a request event ID, get all conversations and take the most recent
                        var conversationsResult = await conversationService.GetConversationsAsync(
                            request.WalletId,
                            project.Id);
                        
                        if (conversationsResult.IsSuccess && conversationsResult.Value.Any())
                        {
                            var latestConversation = conversationsResult.Value
                                .OrderByDescending(c => c.RequestCreated)
                                .FirstOrDefault();
                            conversationResult = Result.Success(latestConversation);
                        }
                        else
                        {
                            conversationResult = Result.Success<InvestmentConversation?>(null);
                        }
                    }

                    if (conversationResult.IsFailure)
                    {
                        logger.LogWarning("Failed to get conversation for project {ProjectId}: {Error}", 
                            project.Id.Value, conversationResult.Error);
                        return Result.Success(dto);
                    }

                    var conversation = conversationResult.Value;
                    if (conversation == null)
                    {
                        // No conversation found, return current dto
                        return Result.Success(dto);
                    }

                    // Update dto with conversation information
                    dto.FounderStatus = conversation.Status == InvestmentRequestStatus.Approved
                        ? FounderStatus.Approved
                        : FounderStatus.Requested;

                    dto.InvestmentStatus = DetermineInvestmentStatus(conversation);

                    if (!string.IsNullOrEmpty(conversation.InvestmentTransactionHex))
                    {
                        var trx = networkConfiguration.GetNetwork()
                            .CreateTransaction(conversation.InvestmentTransactionHex);
                        dto.Investment = new Amount(trx.Outputs.Sum(x => x.Value));
                    }

                    dto.InvestmentId = investmentRecord.InvestmentTransactionHash;

                    return Result.Success(dto);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error processing project {ProjectId}", project.Id.Value);
                    return Result.Failure<InvestedProjectDto>(
                        $"Error processing project {project.Id.Value}: {e.Message}");
                }
            });
            
            var results = await Task.WhenAll(investmentLookupTasks);
            return results.Combine();
        }
        
        private static InvestmentStatus DetermineInvestmentStatus(InvestmentConversation conversation)
        {
            if (string.IsNullOrEmpty(conversation.InvestmentTransactionHex))
                return InvestmentStatus.PendingFounderSignatures;

            if (conversation.Status == InvestmentRequestStatus.Approved)
                return InvestmentStatus.FounderSignaturesReceived;

            return InvestmentStatus.PendingFounderSignatures;
        }
    }
}



