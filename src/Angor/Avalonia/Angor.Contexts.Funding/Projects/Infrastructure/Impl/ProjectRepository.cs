using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;
using Stage = Angor.Contexts.Funding.Projects.Domain.Stage;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class ProjectRepository(
    IRelayService relayService,
    IIndexerService indexerService) : IProjectRepository
{
    public Task<Result<Project>> Get(ProjectId id)
    {
        return TryGet(id).Bind(maybe => maybe.ToResult("Project not found"));
    }

    public async Task<Result<IEnumerable<Project>>> GetAll(params ProjectId[] ids)
    {
        if (ids == null || ids.Length == 0)
            return Result.Success(Enumerable.Empty<Project>());
        
        
        return await GetAllProjectData(ids.ToObservable()
                .Select(id => indexerService.GetProjectByIdAsync(id.Value))
                .Where(x => x.Result != null)
                .Select(x => x.Result!))
            .ToList()
            .Select(x => x.AsEnumerable())
            .ToResult();

    }

    public Task<Result<IList<Project>>> Latest()
    {
        return Result.Try(() => GetAllProjectData(indexerService.GetLatest()).ToList().ToTask());
    }

    public Task<Result<Maybe<Project>>> TryGet(ProjectId projectId)
    {
        return Result.Try(() => indexerService.GetProjectByIdAsync(projectId.Value))
            .Map(data => data.AsMaybe())
            .Map(maybe => maybe.Map(async data => await GetAllProjectData(new[] { data }.ToObservable())));
    }

    private IObservable<Project> GetAllProjectData(IObservable<ProjectIndexerData> projectIndexerDataList)
    {
        return projectIndexerDataList
            .ToList()
            .SelectMany(indexerList =>
            {
                return ProjectInfos(indexerList.Select(info => info.NostrEventId))
                    .Select(x => new Project
                    {
                        Id = new ProjectId(x.ProjectIdentifier),
                        FounderKey = x.FounderKey,
                        ExpiryDate = x.ExpiryDate,
                        FounderRecoveryKey = x.FounderRecoveryKey,
                        NostrPubKey = x.NostrPubKey,
                        PenaltyDuration = TimeSpan.FromDays(x.PenaltyDays),
                        TargetAmount = x.TargetAmount,
                        Stages = x.Stages.Select((stage, i) => new Stage
                        {
                            //Amount =  Convert.ToInt64(x.TargetAmount * stage.AmountToRelease /100),
                            Index = i, 
                            ReleaseDate = stage.ReleaseDate,
                            RatioOfTotal = stage.AmountToRelease
                        }),
                        StartingDate = x.StartDate,
                        EndDate = x.EndDate
                    });
            })
            .ToList()
            .SelectMany(projects =>
            {
                return ProjectMetadatas(projects
                        .Where(x => string.IsNullOrEmpty(x.NostrPubKey) == false)
                        .Select(project => project.NostrPubKey))
                    .Select(projectInfo =>
                    {
                        var project = projects.FirstOrDefault(p => p.NostrPubKey == projectInfo.Item1);
                        if (project == null) return null;
                        project.Banner = Uri.TryCreate(projectInfo.Item2.Banner, UriKind.Absolute, out var bannerUri)
                            ? bannerUri
                            : null;
                        project.InformationUri = Uri.TryCreate(projectInfo.Item2.Website, UriKind.Absolute, out var uri)
                            ? uri
                            : null;
                        project.Picture = Uri.TryCreate(projectInfo.Item2.Picture, UriKind.Absolute, out var pictureUri)
                            ? pictureUri
                            : null;
                        project.Name = projectInfo.Item2.Name;
                        project.ShortDescription = projectInfo.Item2.About;
                        return project;
                    });
            });
    }

    private IObservable<ProjectInfo> ProjectInfos(IEnumerable<string> eventIds)
    {
        return Observable.Create<ProjectInfo>(observer =>
        {
            relayService.LookupProjectsInfoByEventIds<ProjectInfo>(
                observer.OnNext,
                observer.OnCompleted,
                eventIds.ToArray()
            );

            return Disposable.Empty;
        }).Timeout(TimeSpan.FromSeconds(30))
          .Catch<ProjectInfo, Exception>(ex => Observable.Empty<ProjectInfo>());
        
    }

    private IObservable<(string, ProjectMetadata)> ProjectMetadatas(IEnumerable<string> projectInfos)
    {
        return Observable.Create<(string, ProjectMetadata)>(observer =>
        {
            relayService.LookupNostrProfileForNPub(
                (npub, nostrMetadata) => observer.OnNext((npub, nostrMetadata)),
                observer.OnCompleted,
                projectInfos.Where(x => x != null).ToArray());

            return Disposable.Empty;
        }).Timeout(TimeSpan.FromSeconds(30))
          .Catch<(string, ProjectMetadata), Exception>(ex => Observable.Empty<(string, ProjectMetadata)>());
    }
}