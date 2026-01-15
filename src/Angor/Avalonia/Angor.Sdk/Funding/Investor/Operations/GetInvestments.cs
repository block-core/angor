using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Services.Indexer;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class GetInvestments
{
    public record GetInvestmentsRequest(WalletId WalletId) : IRequest<Result<GetInvestmentsResponse>>;
    
    public record GetInvestmentsResponse(IEnumerable<InvestedProjectDto> Projects);
    
    public class GetInvestmentsHandler(
        IAngorIndexerService angorIndexerService,
        IPortfolioService investmentService,
        IProjectService projectService,
        IInvestmentHandshakeService HandshakeService,
        INetworkConfiguration networkConfiguration,
        ILogger<GetInvestmentsHandler> logger
    ) : IRequestHandler<GetInvestmentsRequest, Result<GetInvestmentsResponse>>
    {

        public async Task<Result<GetInvestmentsResponse>> Handle(GetInvestmentsRequest request, CancellationToken cancellationToken)
        {
            var investmentRecordsLookup = await investmentService.GetByWalletId(request.WalletId.Value);

            if (investmentRecordsLookup.IsFailure)
                return Result.Failure<GetInvestmentsResponse>("Failed to retrieve investment records: " + investmentRecordsLookup.Error);
            
            if (investmentRecordsLookup.Value.ProjectIdentifiers.Count == 0)
                return Result.Success(new GetInvestmentsResponse(Enumerable.Empty<InvestedProjectDto>()));

            var ids = investmentRecordsLookup.Value.ProjectIdentifiers
                .Select(id => new ProjectId(id.ProjectIdentifier)).ToArray();

            var lookup = await projectService.GetAllAsync(ids);
            
            if (lookup.IsFailure)
                return Result.Failure<GetInvestmentsResponse>("Failed to retrieve projects: " + lookup.Error);

            var investmentLookupTasks = lookup.Value
                .ToList()
                .OrderByDescending(x => x.StartingDate)
                .Select(async project =>
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

                    // Sync Handshakes from Nostr for this project
                    var syncResult = await HandshakeService.SyncHandshakesFromNostrAsync(
                        request.WalletId,
                        project.Id,
                        project.NostrPubKey);

                    if (syncResult.IsFailure)
                    {
                        logger.LogWarning("Failed to sync Handshakes for project {ProjectId}: {Error}", 
                            project.Id.Value, syncResult.Error);
                    }

                    // Get the Handshake for this specific request event
                    Result<InvestmentHandshake?> HandshakeResult;
                    if (!string.IsNullOrEmpty(investmentRecord.RequestEventId))
                    {
                        HandshakeResult = await HandshakeService.GetHandshakeByRequestEventIdAsync(
                            request.WalletId,
                            project.Id,
                            investmentRecord.RequestEventId);
                    }
                    else
                    {
                        // If we don't have a request event ID, get all Handshakes and take the most recent
                        var HandshakesResult = await HandshakeService.GetHandshakesAsync(
                            request.WalletId,
                            project.Id);
                        
                        if (HandshakesResult.IsSuccess && HandshakesResult.Value.Any())
                        {
                            var latestHandshake = HandshakesResult.Value
                                .OrderByDescending(c => c.RequestCreated)
                                .FirstOrDefault();
                            HandshakeResult = Result.Success(latestHandshake);
                        }
                        else
                        {
                            HandshakeResult = Result.Success<InvestmentHandshake?>(null);
                        }
                    }

                    if (HandshakeResult.IsFailure)
                    {
                        logger.LogWarning("Failed to get Handshake for project {ProjectId}: {Error}", 
                            project.Id.Value, HandshakeResult.Error);
                        return Result.Success(dto);
                    }

                    var Handshake = HandshakeResult.Value;
                    if (Handshake == null)
                    {
                        // No Handshake found, return current dto
                        return Result.Success(dto);
                    }

                    // Update dto with Handshake information
                    dto.FounderStatus = Handshake.Status == InvestmentRequestStatus.Approved
                        ? FounderStatus.Approved
                        : FounderStatus.Requested;

                    dto.InvestmentStatus = DetermineInvestmentStatus(Handshake);

                    if (!string.IsNullOrEmpty(Handshake.InvestmentTransactionHex))
                    {
                        var trx = networkConfiguration.GetNetwork()
                            .CreateTransaction(Handshake.InvestmentTransactionHex);
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
            var combined = results.Combine();
            
            return combined.IsSuccess 
               ? Result.Success(new GetInvestmentsResponse(combined.Value))
                 : Result.Failure<GetInvestmentsResponse>(combined.Error);
        }
        
        private static InvestmentStatus DetermineInvestmentStatus(InvestmentHandshake Handshake)
        {
            if (string.IsNullOrEmpty(Handshake.InvestmentTransactionHex))
                return InvestmentStatus.PendingFounderSignatures;

            if (Handshake.Status == InvestmentRequestStatus.Approved)
                return InvestmentStatus.FounderSignaturesReceived;

            return InvestmentStatus.PendingFounderSignatures;
        }
    }
}



