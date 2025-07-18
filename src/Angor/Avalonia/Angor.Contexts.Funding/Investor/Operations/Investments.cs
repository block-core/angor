using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Zafiro.CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class Investments
{
    public record InvestmentsPortfolioRequest(Guid WalletId) : IRequest<Result<IEnumerable<InvestedProjectDto>>>;
    
    public class InvestmentsPortfolioHandler(
        IIndexerService indexerService,
        IInvestmentRepository investmentRepository,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        IProjectRepository projectRepository,
        ISignService signService,
        INetworkConfiguration networkConfiguration,
        ISerializer serializer ,
        IEncryptionService decrypter
    ) : IRequestHandler<InvestmentsPortfolioRequest,Result<IEnumerable<InvestedProjectDto>>>
    {

        public async Task<Result<IEnumerable<InvestedProjectDto>>> Handle(InvestmentsPortfolioRequest request, CancellationToken cancellationToken)
        {
            var investmentRecordsLookup = await investmentRepository.GetByWalletId(request.WalletId);

            if (investmentRecordsLookup.IsFailure)
                return Result.Failure<IEnumerable<InvestedProjectDto>>("Failed to retrieve investment records: " + investmentRecordsLookup.Error);
            
            if (investmentRecordsLookup.Value.ProjectIdentifiers.Count == 0)
                return Result.Success(Enumerable.Empty<InvestedProjectDto>());

            var ids = investmentRecordsLookup.Value.ProjectIdentifiers
                .Select(id => new ProjectId(id.ProjectIdentifier)).ToArray();

            return await projectRepository.GetAll(ids)
                .ToObservable()
                .SelectMany(projects =>
                {
                    return projects.Value.Select(async project =>
                    {
                        var investmentRecord = investmentRecordsLookup.Value.ProjectIdentifiers
                            .First(x => x.ProjectIdentifier == project.Id.Value);
                        
                        var investment =
                            await GetInvestmentDetails(project.Id.Value, project.FounderKey, request.WalletId);

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
                        };
                        
                        if (investment != null) return Result.Success(dto);

                        var (amount, investmentStatus) = await GetInvestmentStatusFromDms(request.WalletId, project,
                            investmentRecord.RequestEventTime, investmentRecord.RequestEventId);
                        
                        dto.FounderStatus = investmentStatus == InvestmentStatus.FounderSignaturesReceived 
                            ? FounderStatus.Approved : FounderStatus.Requested;
                        dto.Investment = amount;
                        dto.InvestmentStatus = investmentStatus;
                        dto.InvestmentId = investmentRecord.InvestmentTransactionHash;

                        return Result.Success(dto);
                    });
                })
                .Select(x => x.Result)
                .Bind<InvestedProjectDto, InvestedProjectDto>(async investorProjectDto =>
                {
                    var stats = await indexerService.GetProjectStatsAsync(investorProjectDto
                        .Id); // Get project stats for the project ID
                    investorProjectDto.Raised = new Amount(stats.stats?.AmountInvested ?? 0);
                    investorProjectDto.InRecovery = new Amount(stats.stats?.AmountInPenalties ?? 0);
                    return investorProjectDto; // Return the updated InvestedProjectDto with stats
                })
                .Where(x => x.IsSuccess)
                .Select(x => x.Value)
                .ToList()
                .Select(x => x.AsEnumerable())
                .ToResult();
        }

        private async Task<ProjectInvestment?> GetInvestmentDetails(string projectId, string founderKey, Guid walletId)
        {
            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);

            var investorKey = derivationOperations.DeriveInvestorKey(sensitiveDataResult.Value.ToWalletWords(), founderKey);

            var result = Result.Try(async () => await indexerService.GetInvestmentAsync(projectId, investorKey));

            return result.Result.IsSuccess ? result.Result.Value : null;
        }

        private async Task<(Amount, InvestmentStatus)> GetInvestmentStatusFromDms(Guid walletId, Project project,
            DateTime? createdAt = null, string? eventId = null)
        {
            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);
            var pubKey =
                derivationOperations.DeriveNostrPubKey(sensitiveDataResult.Value.ToWalletWords(), project.FounderKey);
            var nostrPrivateKey =
                await derivationOperations.DeriveProjectNostrPrivateKeyAsync(sensitiveDataResult.Value.ToWalletWords(),
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
                
                await signService.LookupInvestmentRequestsAsync(project.NostrPubKey, pubKey, null,
                    async (id, publisherPubKey, content, eventTime) =>
                    {
                        if (createdAt >= eventTime) return;

                        createdAt = eventTime;
                        eventId = id;
                        investmentStatus = InvestmentStatus.PendingFounderSignatures;

                        try
                        {
                            var decrypted =
                                await decrypter.DecryptNostrContentAsync(privateKeyHex, publisherPubKey, content);

                            var investmentRequest = serializer.Deserialize<SignRecoveryRequest>(decrypted);
                            var trx = networkConfiguration.GetNetwork()
                                .CreateTransaction(investmentRequest.InvestmentTransactionHex);

                            amount = new Amount(trx.Outputs.Sum(x => x.Value));
                            investmentId = trx.GetHash().ToString();
                        }
                        catch (Exception e)
                        {
                            requestLookupTcs.SetException(e);
                        }
                    }, () => { requestLookupTcs.SetResult(); });

                await requestLookupTcs.Task;
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



