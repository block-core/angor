using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Stage = Angor.Contexts.Funding.Projects.Domain.Stage;

namespace Angor.Contexts.Funding.Services;

public class DocumentProjectService(IGenericDocumentCollection<Project> collection, IRelayService relayService,
    IIndexerService indexerService) : IProjectService
{

    public Task<Result<Project>> GetAsync(ProjectId id)
    {
        return GetAllAsync( id )
            .Map(x =>x.First());
    }

    public async Task<Result<IEnumerable<Project>>> GetAllAsync(params ProjectId[] ids)
    {
        if (ids == null || ids.Length == 0)
            return Result.Failure<IEnumerable<Project>>("ProjectId cannot be null");

        try
        {
            var stringIds = ids.Select(id => id.Value).ToArray();

            var projectResult = await collection.FindByIdsAsync(stringIds);
            
            var localLookup = projectResult.IsSuccess && projectResult.Value.Any()//check the results from the local database
                ? projectResult.Value.Select(item => item).ToList() : [];

            if (ids.Length == localLookup.Count)
                return Result.Success(localLookup.AsEnumerable()); //all ids are in the local database, return them
            
            var tasks = stringIds
                .Except(localLookup.Select(p => p.Id.Value)) //ids that are not in the local database
                .Select(id => Result.Try(() => indexerService.GetProjectByIdAsync(id)));
            
            var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

            var indexerResults = results //TODO log the failures, we don't need an all or nothing approach here
                .Where(r => r is { IsSuccess: true, Value: not null })
                .Select(r => r.Value!)
                .ToList();
            
            if (indexerResults.Count == 0)
                return Result.Success(localLookup.AsEnumerable());
            
            var nostrEventIds = indexerResults.Select(r => r!.NostrEventId).ToArray();
            var projectInfo = await ProjectInfos(nostrEventIds);
            if (projectInfo.IsFailure || !projectInfo.Value.Any())
                return Result.Failure<IEnumerable<Project>>("Project info not found in relay");

            var metadataResult = await ProjectMetadatas(projectInfo.Value.Select(p => p.NostrPubKey).ToArray());
            if (metadataResult.IsFailure || !metadataResult.Value.Any())
                return Result.Failure<IEnumerable<Project>>("Project metadata not found in relay");

            var lookupList = indexerResults.Select(data =>
            {
                var info = projectInfo.Value.FirstOrDefault(i => i.FounderKey == data.FounderKey);
                if (info is null)
                    return null;
                var metadata = metadataResult.Value.FirstOrDefault(m => m.Npub == info.NostrPubKey)?.NostrMetadata;
                if (metadata is null)
                    return null;

                return new Project
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
                        RatioOfTotal = stage.AmountToRelease
                    }),
                    StartingDate = info.StartDate,
                    EndDate = info.EndDate,
                    Banner = TryGetUri(metadata.Banner),
                    InformationUri = TryGetUri(metadata.Website),
                    Picture = TryGetUri(metadata.Picture),
                    Name = metadata.Name,
                    ShortDescription = metadata.About
                };
            });

            var response = lookupList.Where(p => p != null).Select(p => p!).ToList();

            if (!response.Any())
                return Result.Failure<IEnumerable<Project>>("No projects found");

            var insertResult = await collection.InsertAsync(project => project.Id.Value ,response.ToArray()); //TODO log the result?

            return Result.Success(response.Concat(localLookup));
        }
        catch (Exception ex)
        {
            return Result.Failure<IEnumerable<Project>>(ex.Message);
        }
    }

    public async Task<Result<IEnumerable<Project>>> LatestAsync()
    {
        var top30 = await Result.Try(() => indexerService.GetProjectsAsync(null, 30));

        if (top30.IsFailure)
            return Result.Failure<IEnumerable<Project>>("Failed to retrieve top 20 projects");
        
        var projectIds = top30.Value.Select(p => new ProjectId(p.ProjectIdentifier)).ToArray();
        return await GetAllAsync(projectIds);
    }
    
    
    private Uri? TryGetUri(string uriString)
    {
        return Uri.TryCreate(uriString, UriKind.Absolute, out var uri) ? uri : null;
    }
    
    private Task<Result<IEnumerable<ProjectInfo>>> ProjectInfos(IEnumerable<string> eventIds)
    {
        return Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var tcs = new TaskCompletionSource<List<ProjectInfo>>();
            var results = new List<ProjectInfo>();

            void OnNext(ProjectInfo info) => results.Add(info);
            void OnCompleted() => tcs.SetResult(results);

            relayService.LookupProjectsInfoByEventIds<ProjectInfo>(OnNext, OnCompleted, eventIds.ToArray());

            // Race between completion and timeout
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(10000, cts.Token));
        
            if (completedTask == tcs.Task)
                return Result.Success(results.AsEnumerable());
        
            return Result.Failure<IEnumerable<ProjectInfo>>("Timeout waiting for project info");
        });
    }

    private Task<Result<IEnumerable<ProjectMetadataWithNpub>>> ProjectMetadatas( IEnumerable<string> npubs)
    {
        var projectMetadatas = Observable.Create<ProjectMetadataWithNpub>(observer =>
        {
            relayService.LookupNostrProfileForNPub(
                (npub, nostrMetadata) => observer.OnNext(new ProjectMetadataWithNpub(npub, nostrMetadata)),
                observer.OnCompleted,
                npubs.Where(x => x != null).ToArray());

            return Disposable.Empty;
        }).Timeout(TimeSpan.FromSeconds(30));

        return Result.Try(async () => await projectMetadatas.ToList()).Map(list => list.AsEnumerable());
    }

    private class ProjectMetadataWithNpub : ProjectMetadata
    {
        public string Npub { get; }
        public ProjectMetadata NostrMetadata { get; }
        public ProjectMetadataWithNpub(string npub, ProjectMetadata nostrMetadata)
        {
            Npub = npub; 
            NostrMetadata = nostrMetadata;
        }
    }
}