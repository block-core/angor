using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class ProjectRepository(
    IRelayService relayService,
    IIndexerService indexerService) : IProjectRepository
{
    public Task<Result<Project>> Get(ProjectId id)
    {
        return TryGet(id).Bind(maybe => maybe.ToResult("Project not found"));
    }

    public Task<Result<IList<Project>>> Latest()
    {
        return Result.Try(() => ProjectsFrom(indexerService.GetLatest()).ToList().ToTask());
    }

    public Task<Result<Maybe<Project>>> TryGet(ProjectId projectId)
    {
        return Result.Try(() => indexerService.GetProjectByIdAsync(projectId.Value))
            .Map(data => data.AsMaybe())
            .Map(maybe => maybe.Map(async data => await ProjectsFrom(new[]{ data}.ToObservable())));
    }

    private IObservable<Project> ProjectsFrom(IObservable<ProjectIndexerData> projectIndexerDatas)
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