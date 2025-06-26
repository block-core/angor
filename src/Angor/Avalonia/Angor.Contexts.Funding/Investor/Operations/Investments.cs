using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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
    
    public class InvestmentsPortfolioHandler(
        IRelayService relayService,
        IIndexerService indexerService,
        IInvestmentRepository investmentRepository,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        IProjectRepository projectRepository
    ) : IRequestHandler<InvestmentsPortfolioRequest,Result<IEnumerable<InvestedProjectDto>>>
    {

        public async Task<Result<IEnumerable<InvestedProjectDto>>> Handle(InvestmentsPortfolioRequest request, CancellationToken cancellationToken)
        {
            var investmentRecordsLookup = await investmentRepository.GetByWallet(request.WalletId);

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
                        var investment =
                            await GetInvestmentDetails(project.Id.Value, project.FounderKey, request.WalletId);
                        
                        //TODO need to pull the DMs from the relay and get missing data in case of investmet == null
                        
                        return Result.Success(new InvestedProjectDto
                        {
                            Id = project.Id.Value,
                            Name = project.Name,
                            Description = project.ShortDescription,
                            LogoUri = project.Picture,
                            Target = new Amount(project.TargetAmount),
                            FounderStatus = investment == null ? FounderStatus.Approved : FounderStatus.Invested,
                            Investment = new Amount(investment?.TotalAmount ?? 0) //TODO get the trx from 
                        });
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
            
            
            // return await PullProjectPublicDataPipeline(investmentRecordsLookup)
            //     .ToList()
            //     .Select(x => x.AsEnumerable())
            //     .ToResult();;
        }

        private async Task<ProjectInvestment?> GetInvestmentDetails(string projectId, string founderKey, Guid walletId)
        {
            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);

            var investorKey = derivationOperations.DeriveInvestorKey(sensitiveDataResult.Value.ToWalletWords(), founderKey);

            var result = Result.Try(async () => await indexerService.GetInvestmentAsync(projectId, investorKey));

            return result.Result.IsSuccess ? result.Result.Value : null;
        }
        
        // private IObservable<InvestedProjectDto> PullProjectPublicDataPipeline(Result<InvestmentRecords> investmentRecordsLookup)
        // {
        //     return GatherProjectIdentifiers(investmentRecordsLookup.Value)
        //         .ToList()
        //         .SelectMany(eventIds => GetProjectInfoForEventIds(eventIds.ToArray())) //Get project info for event IDs
        //         .ToList()
        //         .SingleOrDefaultAsync()
        //         .Select(projectInfos => GetProfileForPublicKey(
        //                 projectInfos //Get the profiles for the project public keys
        //                     .Select(result => result.NostrPubKey)
        //                     .ToArray())
        //             .Select(result =>
        //             {
        //                 var projectInfo = projectInfos.First(data => data.NostrPubKey == result.Value.pubKey);
        //
        //                 var profile = result.Value.profile;
        //
        //                 return Result.Success(
        //                     new InvestedProjectDto //Create InvestedProjectDto from project info and profile
        //                     {
        //                         Description = profile.About,
        //                         Name = profile.Name,
        //                         LogoUri = Uri.TryCreate(profile.Website, UriKind.RelativeOrAbsolute, out var uri)
        //                             ? uri
        //                             : null, //TODO handle invalid URIs
        //                         Id = projectInfo.ProjectIdentifier,
        //                         Target = new Amount(projectInfo.TargetAmount),
        //                     });
        //             }))
        //         .Merge()
        //         .Bind<InvestedProjectDto, InvestedProjectDto>(async investorProjectDto =>
        //         {
        //             var stats = await indexerService.GetProjectStatsAsync(investorProjectDto
        //                 .Id); // Get project stats for the project ID
        //             investorProjectDto.Raised = new Amount(stats.stats?.AmountInvested ?? 0);
        //             investorProjectDto.InRecovery = new Amount(stats.stats?.AmountInPenalties ?? 0);
        //             return investorProjectDto; // Return the updated InvestedProjectDto with stats
        //         })
        //         .Where(x => x.IsSuccess)
        //         .Select(x => x.Value);
        // }
        //
        // private IObservable<string> GatherProjectIdentifiers(InvestmentRecords investmentRecordsLookup)
        // {
        //     return investmentRecordsLookup.ProjectIdentifiers.ToObservable()
        //         .ToResult()
        //         .Bind(projectId => Result.Try(() => indexerService.GetProjectByIdAsync(projectId.ProjectIdentifier)))
        //         .Where(x => x.IsSuccess)
        //         .Select(x => x.Value.NostrEventId);
        // }
        //
        //
        // private IObservable<ProjectInfo> GetProjectInfoForEventIds(params string[] eventIds)
        // {
        //     return Observable.Create<ProjectInfo>(observable =>
        //     {
        //         var tcs = new TaskCompletionSource<bool>();
        //         
        //         relayService.LookupProjectsInfoByEventIds<ProjectInfo>(
        //             observable.OnNext, () =>
        //             {
        //                 observable.OnCompleted();
        //                 tcs.SetResult(true);
        //             }, eventIds);
        //
        //         return tcs.Task;
        //     });
        // }
        //
        // private IObservable<Result<(string pubKey,ProjectMetadata profile)>> GetProfileForPublicKey(params string[] publicKey)
        // {
        //     return Observable.Create<Result<(string,ProjectMetadata)>>(observable =>
        //     {
        //         var tcs = new TaskCompletionSource<bool>();
        //         
        //         relayService.LookupNostrProfileForNPub(
        //             (x,y) =>
        //                 Result.Try(() => observable.OnNext((x, y))),
        //             () =>
        //             {
        //                 observable.OnCompleted();
        //                 tcs.SetResult(true);
        //             },
        //             publicKey);
        //         
        //         return tcs.Task;
        //     });
        // }
    }
}



