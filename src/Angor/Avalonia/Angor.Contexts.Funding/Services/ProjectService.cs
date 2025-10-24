using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Zafiro.CSharpFunctionalExtensions;
using Stage = Angor.Contexts.Funding.Projects.Domain.Stage;

namespace Angor.Contexts.Funding.Services;

[Obsolete("Use DocumentProjectRepository instead")]
public class ProjectService(
    IRelayService relayService,
    IIndexerService indexerService,
    [FromKeyedServices("memory")] IStore store) : IProjectService
{
    private Task<Result<Maybe<Project>>> GetSingle(ProjectId id)
    {
        return Get([id]).Map(projects => projects.TryFirst());
    }

    private async Task<Result<IEnumerable<Project>>> Get(params ProjectId[] projectIds)
    {
        var projects = new List<Project>();

        foreach (var projectId in projectIds)
        {
            var cached = await store.Load<Project>(projectId.Value);
            if (cached.IsSuccess)
            {
                projects.Add(cached.Value);
            }
        }   
        
        var lookups = GetProjects(() => GetIndexerDatasIgnoreNotFound(projectIds.Where(x => projects.All(p => p.Id != x))));
        
        var lookupResult = await lookups;
        if (lookupResult.IsFailure)
            return projects.Any()
                ? Result.Success<IEnumerable<Project>>(projects)
                : Result.Failure<IEnumerable<Project>>("Failed to retrieve some projects: " + lookupResult.Error);
        
        
        projects.AddRange(lookupResult.Value);
        foreach (var project in lookupResult.Value)
        {
            await store.Save(project.Id.Value, project);
        }
        
        return Result.Success(projects.AsEnumerable());
    }

    private IEnumerable<Project> Combine(IEnumerable<ProjectIndexerData> indexerItems, IEnumerable<ProjectMetadataWithNpub> metadatas, IEnumerable<ProjectInfo> infos)
    {
        // Inner join by ProjectIdentifier and Nostr pub key; assume both always exist
        var query = from indexer in indexerItems
            join info in infos on indexer.ProjectIdentifier equals info.ProjectIdentifier
            join metadataWithNpub in metadatas on info.NostrPubKey equals metadataWithNpub.Npub
            select new { info, metadata = metadataWithNpub.NostrMetadata };

        return query.Select(item =>
        {
            var info = item.info;
            var metadata = item.metadata;

            var project = new Project
            {
                Id = new ProjectId(info.ProjectIdentifier),
                FounderKey = info.FounderKey,
                ExpiryDate = info.ExpiryDate,
                FounderRecoveryKey = info.FounderRecoveryKey,
                NostrPubKey = info.NostrPubKey,
                PenaltyDuration = TimeSpan.FromDays(info.PenaltyDays),
                TargetAmount = info.TargetAmount,
                Stages = info.Stages.Select((stage, i) => new Stage
                {
                    Index = i,
                    ReleaseDate = stage.ReleaseDate,
                    RatioOfTotal = stage.AmountToRelease / 100m
                }),
                StartingDate = info.StartDate,
                EndDate = info.EndDate,
                Banner = TryGetUri(metadata.Banner),
                InformationUri = TryGetUri(metadata.Website),
                Picture = TryGetUri(metadata.Picture),
                Name = metadata.Name,
                ShortDescription = metadata.About
            };

            return project;
        });
    }

    private Uri? TryGetUri(string uriString)
    {
        return Uri.TryCreate(uriString, UriKind.Absolute, out var uri) ? uri : null;
    }

    private Task<Result<IEnumerable<ProjectInfo>>> GetProjectInfos(IEnumerable<ProjectIndexerData> projectMetadatas)
    {
        return Wrapper.ProjectInfos(relayService, projectMetadatas.Select(data => data.NostrEventId));
    }

    private Task<Result<IEnumerable<ProjectMetadataWithNpub>>> GetProjectMetadatas(IEnumerable<ProjectInfo> indexerData)
    {
        return Wrapper.ProjectMetadatas(relayService, indexerData.Select(data => data.NostrPubKey)).Map(list => list.AsEnumerable());
    }

    private Task<Result<IEnumerable<ProjectIndexerData>>> GetIndexerDatas(IEnumerable<ProjectId> ids)
    {
        return ids
            .Select(id => Result.Try(() => indexerService.GetProjectByIdAsync(id.Value))
                .EnsureNotNull(() => $"Project not found: {id.Value}"))
            .CombineInOrder();
    }


    private Task<Result<IEnumerable<ProjectIndexerData>>> GetIndexerDatasIgnoreNotFound(IEnumerable<ProjectId> ids)
    {
        return ids
            .Select(id => Result.Try(() => indexerService.GetProjectByIdAsync(id.Value).AsMaybe()))
            .CombineInOrder()
            .Map(maybes => maybes.Values());
    }

    public Task<Result<Project>> GetAsync(ProjectId id)
    {
        return GetSingle(id).ToResult($"Project not found: {id.Value}");
    }

    public Task<Result<IEnumerable<Project>>> GetAllAsync(params ProjectId[] ids)
    {
        return Get(ids);
    }

    public Task<Result<IEnumerable<Project>>> LatestAsync()
    {
        return GetProjects(() => Result.Try(() => indexerService.GetProjectsAsync(null, 20)).Map(list => list.AsEnumerable()));
    }

    public Task<Result<Maybe<Project>>> TryGet(ProjectId projectId)
    {
        return GetSingle(projectId);
    }

    private static class Wrapper
    {
            public static Task<Result<IEnumerable<ProjectInfo>>> ProjectInfos(IRelayService relayService, IEnumerable<string> eventIds)
            {
                var projectInfos = Observable.Create<ProjectInfo>(observer =>
                    {
                        relayService.LookupProjectsInfoByEventIds<ProjectInfo>(
                            observer.OnNext,
                            observer.OnCompleted,
                            eventIds.ToArray()
                        );

                        return Disposable.Empty;
                    })
                    .Timeout(TimeSpan.FromSeconds(30));

                return Result.Try(async () => await projectInfos.ToList()).Map(list => list.AsEnumerable());
            }

            public static Task<Result<IEnumerable<ProjectMetadataWithNpub>>> ProjectMetadatas(IRelayService relayService, IEnumerable<string> projectIds)
            {
                var projectMetadatas = Observable.Create<ProjectMetadataWithNpub>(observer =>
                {
                    relayService.LookupNostrProfileForNPub(
                        (npub, nostrMetadata) => observer.OnNext(new ProjectMetadataWithNpub(npub, nostrMetadata)),
                        observer.OnCompleted,
                        projectIds.Where(x => x != null).ToArray());

                    return Disposable.Empty;
                }).Timeout(TimeSpan.FromSeconds(30));

                return Result.Try(async () => await projectMetadatas.ToList()).Map(list => list.AsEnumerable());
            }
    }

    private record ProjectMetadataWithNpub(string Npub, ProjectMetadata NostrMetadata);

    private Task<Result<IEnumerable<Project>>> GetProjects(Func<Task<Result<IEnumerable<ProjectIndexerData>>>> func)
    {
        return from indexerItems in func()
            from infos in GetProjectInfos(indexerItems)
            from metadatas in GetProjectMetadatas(infos)
            select Combine(indexerItems, metadatas, infos);
    }
}