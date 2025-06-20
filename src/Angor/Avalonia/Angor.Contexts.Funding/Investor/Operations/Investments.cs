using System.Reactive.Linq;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Zafiro.CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class Investments
{
    public record InvestmentsPortfolioRequest(Guid WalletId) : IRequest<Result<IEnumerable<InvestedProjectDto>>>;
    
    public class InvestmentsPortfolioHandler(ISeedwordsProvider seedwordsProvider,
        IDerivationOperations _DerivationOperations,
        IRelayService relayService,
        IEncryptionService _encryptionService,
        ISerializer serializer,
        IIndexerService indexerService
    ) : IRequestHandler<InvestmentsPortfolioRequest,Result<IEnumerable<InvestedProjectDto>>>
    {

        public async Task<Result<IEnumerable<InvestedProjectDto>>> Handle(InvestmentsPortfolioRequest request, CancellationToken cancellationToken)
        {
            var result = await RetrieveStorageCredentials(request);
            if (result.IsFailure)
                return Result.Failure<IEnumerable<InvestedProjectDto>>("Failed to retrieve storage credentials: " + result.Error);

            var investmentRecordsLookup = await GetInvestmentRecordsFromRelayTask(result.Value.storageAccountKey, result.Value.password);

            if (investmentRecordsLookup.IsFailure)
                return Result.Failure<IEnumerable<InvestedProjectDto>>("Failed to retrieve investment records: " + investmentRecordsLookup.Error);
            
            if (investmentRecordsLookup.Value.ProjectIdentifiers.Count == 0)
                return Result.Success(Enumerable.Empty<InvestedProjectDto>());
            
            return await GatherProjectIdentifiers(investmentRecordsLookup)
                .ToList()
                .SelectMany(eventIds => GetProjectInfoForEventIds(eventIds.ToArray())) //Get project info for event IDs
                .ToList()
                .SingleOrDefaultAsync()
                .Select(projectInfos => GetProfileForPublicKey(projectInfos //Get the profiles for the project public keys
                        .Select(result => result.NostrPubKey)
                        .ToArray())
                    .Select(result =>
                    {
                        var projectInfo = projectInfos.First(data => data.NostrPubKey == result.Value.pubKey);

                        var profile = result.Value.profile;

                        return Result.Success(new InvestedProjectDto //Create InvestedProjectDto from project info and profile
                        {
                            Description = profile.About,
                            Name = profile.Name,
                            LogoUri = Uri.TryCreate(profile.Website, UriKind.RelativeOrAbsolute, out var uri)
                                ? uri
                                : null, //TODO handle invalid URIs
                            Id = projectInfo.ProjectIdentifier,
                            Target = new Amount(projectInfo.TargetAmount),
                            //TODO
                            //FounderStatus = stats.Value.Value.stats?.FounderStatus ?? FounderStatus.Invalid,
                            //IsInvesmentCompleted = 
                        });
                    }))
                .Merge()
                .Bind<InvestedProjectDto,InvestedProjectDto>(async investorProjectDto =>
                {
                    var stats = await indexerService.GetProjectStatsAsync(investorProjectDto.Id); // Get project stats for the project ID
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

        private async Task<IEnumerable<string>> GatherProjectIdentifiers1(Result<InvestmentRecords> investmentRecordsLookup)
        {
            var projectLookupsTasks = investmentRecordsLookup.Value.ProjectIdentifiers.Select(x =>
                Result.Try(() => indexerService.GetProjectByIdAsync(x.ProjectIdentifier))
            ).ToList();
            
            await Task.WhenAll(projectLookupsTasks);

            return projectLookupsTasks
                .Where(x => x.Result.IsSuccess)
                .Select(x => x.Result.Value.NostrEventId);
        }
        
        private IObservable<string> GatherProjectIdentifiers(Result<InvestmentRecords> investmentRecordsLookup)
        {
            return investmentRecordsLookup.Value.ProjectIdentifiers.ToObservable()
                .ToResult()
                .Bind(projectId => Result.Try(() => indexerService.GetProjectByIdAsync(projectId.ProjectIdentifier)))
                .Where(x => x.IsSuccess)
                .Select(x => x.Value.NostrEventId);
        }

        private async Task<Result<(string storageAccountKey, string password)>> RetrieveStorageCredentials(InvestmentsPortfolioRequest request)
        {
            var words = await seedwordsProvider.GetSensitiveData(request.WalletId);
            if (words.IsFailure)
            {
                return Result.Failure<(string, string)>("Failed to retrieve sensitive data: " + words.Error);
            }
            var storageAccountKey = _DerivationOperations.DeriveNostrStoragePubKeyHex(words.Value.ToWalletWords());
            var password = _DerivationOperations.DeriveNostrStoragePassword(words.Value.ToWalletWords());
            
            return (storageAccountKey, password);
        }

        private Task<Result<InvestmentRecords>> GetInvestmentRecordsFromRelayTask(string storageAccountKey,
            string password)
        {
            var tcs = new TaskCompletionSource<Result<InvestmentRecords>>();
            
            relayService.LookupDirectMessagesForPubKey(storageAccountKey, null, 1, async (nostrEvent) =>
            {
                try
                {
                    var decrypted = await _encryptionService.DecryptData(nostrEvent.Content, password);
                    var investmentRecords = serializer.Deserialize<InvestmentRecords>(decrypted);
                    tcs.SetResult(investmentRecords);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }

            }, new[] { storageAccountKey });

            return tcs.Task;
        }

        private IObservable<ProjectInfo> GetProjectInfoForEventIds(params string[] eventIds)
        {
            return Observable.Create<ProjectInfo>(observable =>
            {
                var tcs = new TaskCompletionSource<bool>();
                
                relayService.LookupProjectsInfoByEventIds<ProjectInfo>(
                    observable.OnNext, () =>
                    {
                        observable.OnCompleted();
                        tcs.SetResult(true);
                    }, eventIds);

                return tcs.Task;
            });
        }
        
        private IObservable<Result<(string pubKey,ProjectMetadata profile)>> GetProfileForPublicKey(params string[] publicKey)
        {
            return Observable.Create<Result<(string,ProjectMetadata)>>(observable =>
            {
                var tcs = new TaskCompletionSource<bool>();
                
                relayService.LookupNostrProfileForNPub(
                    (x,y) =>
                        Result.Try(() => observable.OnNext((x, y))),
                    () =>
                    {
                        observable.OnCompleted();
                        tcs.SetResult(true);
                    },
                    publicKey);
                
                return tcs.Task;
            });
        }
    }
}



