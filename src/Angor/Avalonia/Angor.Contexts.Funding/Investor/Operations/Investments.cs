using System.Reactive.Linq;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;

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
            var words = await seedwordsProvider.GetSensitiveData(request.WalletId);
            var storageAccountKey = _DerivationOperations.DeriveNostrStoragePubKeyHex(words.Value.ToWalletWords());
            var password = _DerivationOperations.DeriveNostrStoragePassword(words.Value.ToWalletWords());
            
            if (string.IsNullOrEmpty(storageAccountKey) || string.IsNullOrEmpty(password))
            {
                return Result.Failure<IEnumerable<InvestedProjectDto>>("Storage account key or password is empty.");
            }

            var investmentRecordsLookup = await GetInvestmentRecordsFromRelayTask(storageAccountKey, password);

            if (investmentRecordsLookup.IsFailure)
                return Result.Failure<IEnumerable<InvestedProjectDto>>("Failed to retrieve investment records: " + investmentRecordsLookup.Error);

            var projectLookupsTasks = investmentRecordsLookup.Value.ProjectIdentifiers.Select(x =>
                 Result.Try(() => indexerService.GetProjectByIdAsync(x.ProjectIdentifier))
            ).ToList();

            var projectStatsTasks = investmentRecordsLookup.Value.ProjectIdentifiers.Select(x =>
                Result.Try(() => indexerService.GetProjectStatsAsync(x.ProjectIdentifier))
            ).ToList();
            
            await Task.WhenAll(projectLookupsTasks);

            var eventIds = projectLookupsTasks
                .Where(x => x.Result.IsSuccess)
                .Select(x => x.Result.Value.NostrEventId)
                .ToArray();

            if (eventIds.Length == 0)
                return Result.Success(Enumerable.Empty<InvestedProjectDto>());


            var projectInfos = await GetProjectInfoForEventIds(eventIds)
                .ToList();

            var metadatas = await GetProfileForPublicKey(projectInfos
                    .Select(result => result.NostrPubKey)
                    .ToArray())
                .ToList();

            await Task.WhenAll(projectStatsTasks);

            return Result.Try(() =>  metadatas
                .Where(x => x.IsSuccess)
                .Select(tuple =>
            {
                var projectInfo = projectInfos.FirstOrDefault(data => data.NostrPubKey == tuple.Value.pubKey);
                if (projectInfo == null)
                    return null;

                var stats = projectStatsTasks
                    .FirstOrDefault(x => x.Result.IsSuccess && x.Result.Value.projectId == projectInfo.ProjectIdentifier)?.Result;
                    
                if (stats == null || stats.HasValue && stats.Value.IsFailure)
                    return null;
                
                return new InvestedProjectDto
                {
                    Description = tuple.Value.profile.About,
                    Name = tuple.Value.profile.Name,
                    LogoUri = Uri.TryCreate(tuple.Value.profile.Website, UriKind.RelativeOrAbsolute, out var uri)
                        ? uri
                        : null, //TODO handle invalid URIs
                    Id = projectInfo.ProjectIdentifier,
                    Target = new Amount(projectInfo.TargetAmount),
                    Raised = new Amount(stats.Value.Value.stats?.AmountInvested ?? 0),
                    InRecovery = new Amount(stats.Value.Value.stats?.AmountInPenalties ?? 0 ),
                   //TODO
                    //FounderStatus = stats.Value.Value.stats?.FounderStatus ?? FounderStatus.Invalid,
                   //IsInvesmentCompleted = 
                };
            }));
            //
            //
            // var investedProjectDtos = await GetProjectInfoForEventIds(eventIds)
            //     .ToList()
            //     .SelectMany(resultListOfProjectInfo => //<IList<ProjectInfo>, (ProjectMetadata profile, ProjectInfo projectInfo)>
            //         GetProfileForPublicKey(resultListOfProjectInfo.Select(result => result.NostrPubKey).ToArray())
            //             .Map(tuple =>
            //             {
            //                 var projectInfo = resultListOfProjectInfo
            //                     .FirstOrDefault(data => data.NostrPubKey == tuple.pubKey);
            //                 var profile = tuple.profile;
            //                 return new InvestedProjectDto
            //                 {
            //                     Description = profile.About,
            //                     Name = profile.Name,
            //                     LogoUri = Uri.TryCreate(profile.Website, UriKind.RelativeOrAbsolute, out var uri)
            //                         ? uri
            //                         : null, //TODO handle invalid URIs
            //                     Id = projectInfo.ProjectIdentifier,
            //                     Target = new Amount(projectInfo.TargetAmount),
            //                 };
            //             }))//.ToTask(cancellationToken))
            //        //     .Select(x => x.Value))
            //     //.Merge()
            //     // .Map<(ProjectMetadata profile,ProjectInfo projectInfo),InvestedProjectDto?>(x =>
            //     // {
            //     //     if (x.IsFailure )
            //     //         return null;
            //     //     
            //     //     var profile = x.Value.profile;
            //     //     var projectInfo = x.Value.projectInfo;
            //     //     return new InvestedProjectDto
            //     //     {
            //     //         Description = profile.About,
            //     //         Name = profile.Name,
            //     //         LogoUri = Uri.TryCreate(profile.Website, UriKind.RelativeOrAbsolute, out var uri) ? uri : null, //TODO handle invalid URIs
            //     //         Id = projectInfo.ProjectIdentifier,
            //     //         Target = new Amount(projectInfo.TargetAmount),
            //     //     };
            //     // })
            //     //.Where(x => x.IsSuccess)
            //    // .Select(x => x!)
            //     .Where(x => x.IsSuccess)
            //     .Select(x => x.Value)
            //     .ToList()
            //     .ToTask(cancellationToken);
            //
            // await Task.WhenAll(projectStatsTasks);
            //
            // foreach (var lookupsTask in projectStatsTasks)
            // {
            //     var investmentProject = investedProjectDtos
            //         .FirstOrDefault(x => x. Id == lookupsTask.Result.Value.projectId);
            //
            //     if (lookupsTask.Result.IsSuccess || lookupsTask.Result.Value.stats == null)
            //         continue;
            //     
            //     investmentProject.Raised = new Amount(lookupsTask.Result.Value.stats.AmountInvested);
            //     investmentProject.InRecovery = new Amount(lookupsTask.Result.Value.stats.AmountInPenalties);
            // }
            //
            // return Result.Success(investedProjectDtos.AsEnumerable());
            
            // var projectsTasks = TestAsync.Value.ProjectIdentifiers.Select(x =>
            //     Result.Try(() => indexerService.GetProjectByIdAsync(x.ProjectIdentifier)));
            //
            // var statsTasks = TestAsync.Value.ProjectIdentifiers.Select(x =>
            //     Result.Try(() => indexerService.GetProjectStatsAsync(x.ProjectIdentifier)));
            //
            // var InvestmentStats = TestAsync.Value.ProjectIdentifiers.Select(x => 
            //     Result.Try(() => indexerService.GetInvestmentAsync(x.ProjectIdentifier, x.InvestorPubKey)));
            //
            // var projectInfos = TestAsync.Value.ProjectIdentifiers.Select(x => x.e)
            //
            // var investmentRecords = await GetInvestmentRecordsFromRelay(storageAccountKey, password)
            //     .SingleOrDefaultAsync()
            //     .Bind(x =>  
            //         x.ProjectIdentifiers
            //             .Select(record => 
            //                 Result.Try(() => indexerService.GetProjectStatsAsync(record.ProjectIdentifier))
            //                     .ToObservable()
            //                     .Map(projectStats => new { projectStats,record}))
            //             .Merge()
            //             .ToTask(cancellationToken))
            //     .Bind(x => Result.Try(() => relayService.LookupNostrProfileForNPub(x.projectStats.)) )
            //
            //
            //
            // var investmentRecords = await GetInvestmentRecordsFromRelay(storageAccountKey, password)
            //     .SingleOrDefaultAsync()
            //     .Bind(x => Result.Try(() => 
            //         x.ProjectIdentifiers
            //             .Select<InvestorPositionRecord, IObservable<ProjectInvestment?>>(record => 
            //                 indexerService.GetInvestmentAsync(record.ProjectIdentifier, record.InvestorPubKey)
            //             .ToObservable())
            //         .Merge()))
            //     .Bind(x => relayService.LookupNostrProfileForNPub())
            //     // .Select(x => new InvestedProjectDto
            //     // {
            //     //     Description = x!.Description,
            //     //     ProjectId = x.ProjectId,
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



