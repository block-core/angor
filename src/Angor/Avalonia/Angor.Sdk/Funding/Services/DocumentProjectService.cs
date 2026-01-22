using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;
using Angor.Shared.Services.Indexer;

namespace Angor.Sdk.Funding.Services;

public class DocumentProjectService(
  IGenericDocumentCollection<Project> collection,
    IRelayService relayService,
    IAngorIndexerService angorIndexerService) : IProjectService
{
    public Task<Result<Project>> GetAsync(ProjectId id)
    {
        return TryGetAsync(id).Bind(maybe => maybe.ToResult($"Project with id {id} not found"));
    }

    public Task<Result<Maybe<Project>>> TryGetAsync(ProjectId id)
    {
        return GetAllAsync(id).Map(x => x.TryFirst());
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
                return Result.Success(localLookup.OrderByDescending(p => p.StartingDate).AsEnumerable()); //all ids are in the local database, return them

            var tasks = stringIds
                   .Except(localLookup.Select(p => p.Id.Value)) //ids that are not in the local database
                   .Select(id => Result.Try(() => angorIndexerService.GetProjectByIdAsync(id)));

            var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

            var indexerResults = results //TODO log the failures, we don't need an all or nothing approach here
                 .Where(r => r is { IsSuccess: true, Value: not null })
                 .Select(r => r.Value!)
                 .ToList();

            if (indexerResults.Count == 0)
                return Result.Success(localLookup.OrderByDescending(p => p.StartingDate).AsEnumerable());

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
                             PenaltyThreshold = info.PenaltyThreshold,
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
                             ShortDescription = metadata.About,

                             // New properties from ProjectInfo
                             Version = info.Version,
                             ProjectType = info.ProjectType,
                             DynamicStagePatterns = info.DynamicStagePatterns ?? new List<DynamicStagePattern>()
                         };
                     });

            var response = lookupList.Where(p => p != null).Select(p => p!).OrderByDescending(p => p.StartingDate).ToList();

            if (!response.Any())
                return Result.Failure<IEnumerable<Project>>("No projects found");

            var insertResult = await collection.InsertAsync(project => project.Id.Value, response.ToArray()); //TODO log the result?

            return Result.Success(response.Concat(localLookup));
        }
        catch (Exception ex)
        {
            return Result.Failure<IEnumerable<Project>>(ex.Message);
        }
    }

    public async Task<Result<IEnumerable<Project>>> LatestAsync()
    {
        var top30 = await Result.Try(() => angorIndexerService.GetProjectsAsync(null, 30));

        if (top30.IsFailure)
            return Result.Failure<IEnumerable<Project>>("Failed to retrieve top 30 projects");

        var projectIds = top30.Value.Select(p => new ProjectId(p.ProjectIdentifier)).ToArray();
        return await GetAllAsync(projectIds);
    }

    public async Task<Result<IEnumerable<Project>>> LatestFromNostrAsync()
    {
        // Step 1: Query Nostr relays for the latest 30 kind 3030 events
        var nostrProjectsResult = await QueryLatestNostrProjectEventsAsync(30);
        if (nostrProjectsResult.IsFailure)
            return Result.Failure<IEnumerable<Project>>(nostrProjectsResult.Error);

        var nostrProjects = nostrProjectsResult.Value.ToList();
        if (!nostrProjects.Any())
            return Result.Failure<IEnumerable<Project>>("No projects found in Nostr relays");

        // Step 2: Validate each project exists on-chain in parallel
        var validationTasks = nostrProjects
            .Where(p => !string.IsNullOrEmpty(p.Data.ProjectIdentifier))
            .Select(async eventInfo =>
            {
                try
                {
                    var indexerData = await angorIndexerService.GetProjectByIdAsync(eventInfo.Data.ProjectIdentifier);

                    if (indexerData?.NostrEventId != eventInfo.EventId)
                        return null; // Mismatch between Nostr event ID and indexer data

                    return indexerData != null ? new ProjectId(eventInfo.Data.ProjectIdentifier) : null;
                }
                catch
                {
                    // Skip projects that fail validation
                    return null;
                }
            });

        var results = await Task.WhenAll(validationTasks);
        var validatedProjectIds = results.Where(id => id != null).Select(id => id!).ToList();

        if (!validatedProjectIds.Any())
            return Result.Failure<IEnumerable<Project>>("No valid on-chain projects found from Nostr events");

        // Step 3: Use existing GetAllAsync to fetch full project data with caching
        return await GetAllAsync(validatedProjectIds.ToArray());
    }

    private Task<Result<IEnumerable<EventInfo<ProjectInfo>>>> QueryLatestNostrProjectEventsAsync(int limit)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var tcs = new TaskCompletionSource<Dictionary<string, EventInfo<ProjectInfo>>>();
        var results = new Dictionary<string, EventInfo<ProjectInfo>>();

        void OnNext(EventInfo<ProjectInfo> eventInfo)
        {
            // Deduplicate by project identifier using dictionary key
            if (!string.IsNullOrEmpty(eventInfo.Data.ProjectIdentifier))
            {
                results.TryAdd(eventInfo.Data.ProjectIdentifier, eventInfo);
            }
        }

        void OnCompleted() => tcs.TrySetResult(results);

        relayService.LookupLatestProjects<ProjectInfo>(OnNext, OnCompleted, limit);

        // Race between completion and timeout
        var completedTask = Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10), cts.Token));

        if (completedTask.Result == tcs.Task)
            return Task.FromResult(Result.Success(results.Values.AsEnumerable()));

        // On timeout, return whatever we collected
        return Task.FromResult(results.Any()
               ? Result.Success(results.Values.AsEnumerable())
               : Result.Failure<IEnumerable<EventInfo<ProjectInfo>>>("Timeout waiting for Nostr project events"));
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

    private Task<Result<IEnumerable<ProjectMetadataWithNpub>>> ProjectMetadatas(IEnumerable<string> npubs)
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