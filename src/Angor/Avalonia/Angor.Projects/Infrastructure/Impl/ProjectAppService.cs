using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Projects.Application.Dtos;
using Angor.Projects.Domain;
using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace Angor.Projects.Infrastructure.Impl;

public class ProjectAppService(
    IProjectRepository projectRepository,
    IInvestmentRepository investmentRepository,
    IInvestmentService bitcoinService, 
    IRelayService relayService,
    IIndexerService indexerService)
    : IProjectAppService
{
    public Task<IList<ProjectDto>> Latest()
    {
        return ProjectsFrom(indexerService.GetLatest()).ToList().ToTask();
    }
    
    public async Task<Maybe<ProjectDto>> FindById(string projectId)
    {
        var project = (await indexerService.GetProjectByIdAsync(projectId)).AsMaybe();
        return await project.Map(async data => await ProjectsFrom(new[] { data }.ToObservable()).FirstAsync());
    }
    
    public async Task<Result> Invest(ProjectId projectId, Amount amount, ModelFeeRate feeRate)
    {
        var projectResult = await projectRepository.Get(projectId);
        if (projectResult.IsFailure)
        {
            return Result.Failure(projectResult.Error);
        }
        
        var project = projectResult.Value;
        
        // 2. Get investor data
        string investorId = "..."; 
        string investorPubKey = "...";
        string projectAddress = "...";
        
        // 3. Create invest transaction
        var transactionResult = await bitcoinService.CreateInvestmentTransaction(
            projectAddress, 
            investorPubKey,
            amount.Sats,
            feeRate
        );
        
        if (transactionResult.IsFailure)
        {
            return Result.Failure(transactionResult.Error);
        }
        
        // 4. Create and save investment
        var investment = Investment.Create(project.Id, investorId, amount.Sats);
        await investmentRepository.Save(investment);
        
        return Result.Success();
    }
    
    public IObservable<ProjectDto> ProjectsFrom(IObservable<ProjectIndexerData> projectIndexerDatas)
    {
        var tuples = projectIndexerDatas.ToList().SelectMany(lists => ProjectInfos(lists)
            .ToList()
            .SelectMany(projectInfos => ProjectMetadatas(projectInfos).ToList().Select(metadatas => new
            {
                metadatas, projectInfos, projectIndexerDatas = lists
            })));

        var observable = tuples.Select(x =>
        {
            var infoAndMetadata = x.projectInfos.Join(x.metadatas, projectInfo => projectInfo.NostrPubKey, tuple => tuple.Item1, (info, tuple) => (info, Metadata: tuple.Item2));
            return x.projectIndexerDatas
                .Join(
                    infoAndMetadata,
                    projectIndexerData => projectIndexerData.ProjectIdentifier,
                    tuple => tuple.info.ProjectIdentifier,
                    (projectIndexerData, tuple) => new ProjectData
                    {
                        ProjectInfo = tuple.info,
                        NostrMetadata = tuple.Metadata,
                        IndexerData = projectIndexerData
                    }).Select(data => data.ToProject());
        });

        return observable.Flatten();
    }

    private IObservable<ProjectInfo> ProjectInfos(IEnumerable<ProjectIndexerData> projectIndexerDatas)
    {
        return Observable.Create<ProjectInfo>(observer =>
        {
            relayService.LookupProjectsInfoByEventIds<ProjectInfo>(
                observer.OnNext,
                observer.OnCompleted,
                projectIndexerDatas.Select(x => x.NostrEventId).ToArray()
            );

            return Disposable.Empty;
        });
    }

    private IObservable<(string, ProjectMetadata)> ProjectMetadatas(IEnumerable<ProjectInfo> projectInfos)
    {
        return Observable.Create<(string, ProjectMetadata)>(observer =>
        {
            relayService.LookupNostrProfileForNPub((npub, nostrMetadata) => observer.OnNext((npub, nostrMetadata)), observer.OnCompleted, projectInfos.Select(x => x.NostrPubKey).ToArray());

            return Disposable.Empty;
        });
    }
}