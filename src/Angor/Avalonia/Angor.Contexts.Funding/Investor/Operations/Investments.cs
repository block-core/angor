using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Services;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class Investments
{
    public record InvestmentsPortfolioRequest(Guid WalletId) : IRequest<Result<IEnumerable<InvestedProjectDto>>>;
    
    public class InvestmentsPortfolioHandler(
        IIndexerService indexerService,
        IPortfolioService investmentService,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        IProjectService projectService,
        ISignService signService,
        INetworkConfiguration networkConfiguration,
        ISerializer serializer ,
        IEncryptionService decrypter
    ) : IRequestHandler<InvestmentsPortfolioRequest,Result<IEnumerable<InvestedProjectDto>>>
    {

        public async Task<Result<IEnumerable<InvestedProjectDto>>> Handle(InvestmentsPortfolioRequest request, CancellationToken cancellationToken)
        {
            var investmentRecordsLookup = await investmentService.GetByWalletId(request.WalletId);

            if (investmentRecordsLookup.IsFailure)
                return Result.Failure<IEnumerable<InvestedProjectDto>>("Failed to retrieve investment records: " + investmentRecordsLookup.Error);
            
            if (investmentRecordsLookup.Value.ProjectIdentifiers.Count == 0)
                return Result.Success(Enumerable.Empty<InvestedProjectDto>());

            var ids = investmentRecordsLookup.Value.ProjectIdentifiers
                .Select(id => new ProjectId(id.ProjectIdentifier)).ToArray();

            var lookup = await projectService.GetAllAsync(ids);
            
            if (lookup.IsFailure)
                return Result.Failure<IEnumerable<InvestedProjectDto>>("Failed to retrieve projects: " + lookup.Error);

            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(request.WalletId);

            var investmentLookupTasks = lookup.Value.ToList().Select(async project =>
            {
                try
                {
                    var investmentRecord = investmentRecordsLookup.Value.ProjectIdentifiers
                        .First(x => x.ProjectIdentifier == project.Id.Value);

                    var investorKey = derivationOperations.DeriveInvestorKey(sensitiveDataResult.Value.ToWalletWords(),
                        project.FounderKey);

                    var investmentTask = Result.Try(() => indexerService.GetInvestmentAsync(project.Id.Value, investorKey));
                    var statsTask = Result.Try(() => 
                        indexerService.GetProjectStatsAsync(project.Id.Value)); // Get project stats for the project ID

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

                    var (amount, investmentStatus) = await GetInvestmentStatusFromDms(
                        sensitiveDataResult.Value.ToWalletWords(), project,
                        investmentRecord.RequestEventTime, investmentRecord.RequestEventId);

                    dto.FounderStatus = investmentStatus == InvestmentStatus.FounderSignaturesReceived
                        ? FounderStatus.Approved
                        : FounderStatus.Requested;
                    dto.Investment = amount;
                    dto.InvestmentStatus = investmentStatus;
                    dto.InvestmentId = investmentRecord.InvestmentTransactionHash;

                    return Result.Success(dto);
                }
                catch (Exception e)
                {
                    return Result.Failure<InvestedProjectDto>(
                        $"Error processing project {project.Id.Value}: {e.Message}");
                }
            });
            
            var results = await Task.WhenAll(investmentLookupTasks);
            return results.Combine();
        }
        
        private async Task<(Amount, InvestmentStatus)> GetInvestmentStatusFromDms(WalletWords words, Project project,
            DateTime? createdAt = null, string? eventId = null)
        {
            var pubKey =
                derivationOperations.DeriveNostrPubKey(words, project.FounderKey);
            var nostrPrivateKey =
                await derivationOperations.DeriveProjectNostrPrivateKeyAsync(words,
                    project.FounderKey);

            var privateKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());

            var investmentStatus = InvestmentStatus.Invalid;
            var amount = new Amount(0);
            var investmentId = string.Empty;


            if (createdAt == null && eventId == null) //If we don't have a createdAt or eventId, we need to fetch it from NOSTR
            {
                createdAt = DateTime.MinValue;
                eventId = string.Empty;

                var requestLookupTcs = new TaskCompletionSource();
                var encryptedContent = string.Empty;
                var investorPubKey = string.Empty;
                await signService.LookupInvestmentRequestsAsync(project.NostrPubKey, pubKey, null,
                    async (id, publisherPubKey, content, eventTime) =>
                    {
                        if (createdAt >= eventTime) return;

                        createdAt = eventTime;
                        eventId = id;
                        investmentStatus = InvestmentStatus.PendingFounderSignatures;
                        encryptedContent = content;
                        investorPubKey = publisherPubKey;
                    }, () => { requestLookupTcs.SetResult(); });

                await requestLookupTcs.Task;
                
                try
                {
                    var decrypted =
                        await decrypter.DecryptNostrContentAsync(privateKeyHex, investorPubKey, encryptedContent);

                    var investmentRequest = serializer.Deserialize<SignRecoveryRequest>(decrypted);
                    var trx = networkConfiguration.GetNetwork()
                        .CreateTransaction(investmentRequest.InvestmentTransactionHex);

                    amount = new Amount(trx.Outputs.Sum(x => x.Value));
                }
                catch (Exception e)
                {
                    requestLookupTcs.SetException(e);
                }
            }
            else
            {
                //If we have the event id we know we sent a request to the founder
                investmentStatus = InvestmentStatus.PendingFounderSignatures;
            }

            if (createdAt == DateTime.MinValue || string.IsNullOrEmpty(eventId))
            {
                // If we still don't have a createdAt or eventId, we cannot proceed
                return (amount, InvestmentStatus.Invalid);
            }

            var tcs = new TaskCompletionSource<InvestmentStatus>();


            signService.LookupSignatureForInvestmentRequest(pubKey, project.NostrPubKey, createdAt, eventId,
                signature =>
                {
                    investmentStatus = InvestmentStatus.FounderSignaturesReceived;
                    return tcs.Task;
                },
                () => { tcs.SetResult(investmentStatus); });

            await tcs.Task;

            return (amount, investmentStatus);
        }
    }
}



