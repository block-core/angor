using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Funding.Services;

public class DocumentProjectService(
  IGenericDocumentCollection<Project> collection,
    IRelayService relayService,
    IAngorIndexerService angorIndexerService,
    ILogger<DocumentProjectService> logger) : IProjectService
{
    /// <summary>How long cached Nostr profile metadata is considered fresh.
    /// Stale entries are served immediately and revalidated in the background
    /// (stale-while-revalidate), so founder profile updates propagate without
    /// re-querying relays on every read.</summary>
    private static readonly TimeSpan MetadataTtl = TimeSpan.FromHours(1);

    /// <summary>Project ids with an in-flight background metadata revalidation.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> RevalidationsInFlight = new();

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

            List<Project> localLookup;
            if (projectResult.IsFailure)
            {
                logger.LogWarning("Failed to read projects from local cache: {Error}. Treating as empty cache.", projectResult.Error);
                localLookup = [];
            }
            else
            {
                localLookup = projectResult.Value.ToList();
            }

            // Stale-while-revalidate: cached docs are returned immediately below, but any
            // whose profile metadata is older than the TTL get refreshed in the background
            // so banner/avatar/name changes eventually propagate to every reader.
            RevalidateStaleMetadata(localLookup);

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
                                 // AmountToRelease is 0-100 percentage; RatioOfTotal is 0-1 ratio
                                 RatioOfTotal = stage.AmountToRelease / 100m
                             }),
                             StartingDate = info.StartDate,
                             EndDate = info.EndDate,
                             Banner = TryGetUri(metadata.Banner),
                             InformationUri = TryGetUri(metadata.Website),
                             Picture = TryGetUri(metadata.Picture),
                             Name = metadata.Name,
                             ShortDescription = metadata.About,
                             MetadataFetchedAt = DateTime.UtcNow,

                              // New properties from ProjectInfo
                              Version = info.Version,
                              ProjectType = info.ProjectType,
                              DynamicStagePatterns = info.DynamicStagePatterns ?? new List<DynamicStagePattern>(),
                              NetworkName = info.NetworkName
                         };
                     });

            var response = lookupList.Where(p => p != null).Select(p => p!).OrderByDescending(p => p.StartingDate).ToList();

            if (!response.Any())
                return Result.Failure<IEnumerable<Project>>("No projects found");

            foreach (var project in response)
            {
                var upsertResult = await collection.UpsertAsync(p => p.Id.Value, project);
                if (upsertResult.IsFailure)
                    logger.LogWarning("Failed to cache project {ProjectId}: {Error}", project.Id.Value, upsertResult.Error);
            }

            return Result.Success(response.Concat(localLookup).OrderByDescending(p => p.StartingDate).AsEnumerable());
        }
        catch (Exception ex)
        {
            return Result.Failure<IEnumerable<Project>>(ex.Message);
        }
    }

    /// <summary>
    /// Drop a project's cached document so the next read refetches everything from
    /// the indexer and relays. Used after the founder publishes a profile update so
    /// their own app reflects the change immediately.
    /// </summary>
    public async Task<Result> InvalidateCacheAsync(ProjectId id)
    {
        var result = await collection.DeleteAsync(id.Value);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }

    /// <summary>
    /// Background revalidation for cached projects whose profile metadata is older
    /// than <see cref="MetadataTtl"/>. Fetches fresh kind-0 metadata from relays
    /// (using the cached NostrPubKey — no indexer round-trip needed) and upserts the
    /// profile fields. Fire-and-forget; failures only log.
    /// </summary>
    private void RevalidateStaleMetadata(List<Project> cached)
    {
        var now = DateTime.UtcNow;
        var stale = cached
            .Where(p => !string.IsNullOrEmpty(p.NostrPubKey))
            .Where(p => now - p.MetadataFetchedAt > MetadataTtl)
            .Where(p => RevalidationsInFlight.TryAdd(p.Id.Value, 0))
            .ToList();

        if (stale.Count == 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                var metadataResult = await ProjectMetadatas(stale.Select(p => p.NostrPubKey).Distinct().ToArray());
                if (metadataResult.IsFailure)
                {
                    logger.LogDebug("Metadata revalidation fetch failed: {Error}", metadataResult.Error);
                    return;
                }

                foreach (var project in stale)
                {
                    var metadata = metadataResult.Value
                        .FirstOrDefault(m => m.Npub == project.NostrPubKey)?.NostrMetadata;
                    if (metadata == null)
                        continue; // no profile event received — keep cached values, retry after TTL

                    project.Name = metadata.Name;
                    project.ShortDescription = metadata.About;
                    project.Picture = TryGetUri(metadata.Picture);
                    project.Banner = TryGetUri(metadata.Banner);
                    project.InformationUri = TryGetUri(metadata.Website);
                    project.MetadataFetchedAt = DateTime.UtcNow;

                    var upsert = await collection.UpsertAsync(p => p.Id.Value, project);
                    if (upsert.IsFailure)
                        logger.LogWarning("Failed to persist revalidated metadata for {ProjectId}: {Error}",
                            project.Id.Value, upsert.Error);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Background metadata revalidation failed");
            }
            finally
            {
                foreach (var project in stale)
                    RevalidationsInFlight.TryRemove(project.Id.Value, out _);
            }
        });
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
            .Where(p => p != null && !string.IsNullOrEmpty(p.Data?.ProjectIdentifier))
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

    private async Task<Result<IEnumerable<ProjectInfo>>> ProjectInfos(IEnumerable<string> eventIds)
    {
        var expectedIds = eventIds.ToHashSet();
        var expectedCount = expectedIds.Count;

        var tcs = new TaskCompletionSource<List<ProjectInfo>>();
        var results = new List<ProjectInfo>();
        var receivedIds = new HashSet<string>();

        void OnNext(ProjectInfo info)
        {
            results.Add(info);

            // Track by ProjectIdentifier to deduplicate across relays
            if (info.ProjectIdentifier != null && receivedIds.Add(info.ProjectIdentifier))
            {
                // Complete early once we have info for all requested events
                if (receivedIds.Count >= expectedCount)
                {
                    tcs.TrySetResult(results);
                }
            }
        }

        void OnCompleted() => tcs.TrySetResult(results);

        relayService.LookupProjectsInfoByEventIds<ProjectInfo>(OnNext, OnCompleted, expectedIds.ToArray());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        cts.Token.Register(() => tcs.TrySetResult(results));

        var completedResults = await tcs.Task;
        return Result.Success(completedResults.AsEnumerable());
    }

    private Task<Result<IEnumerable<ProjectMetadataWithNpub>>> ProjectMetadatas(IEnumerable<string> npubs)
    {
        var npubSet = npubs.Where(x => x != null).ToHashSet();
        var expectedCount = npubSet.Count;

        var projectMetadatas = Observable.Create<ProjectMetadataWithNpub>(observer =>
        {
            var received = new HashSet<string>();

            relayService.LookupNostrProfileForNPub(
                   (npub, nostrMetadata) =>
                   {
                       // Only emit distinct npubs (relays may send duplicates)
                       if (received.Add(npub))
                       {
                           observer.OnNext(new ProjectMetadataWithNpub(npub, nostrMetadata));

                           // Complete early once we have metadata for all requested npubs
                           if (received.Count >= expectedCount)
                           {
                               observer.OnCompleted();
                           }
                       }
                   },
                    observer.OnCompleted,
                    npubSet.ToArray());

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