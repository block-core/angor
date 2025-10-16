using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Stage = Angor.Contexts.Funding.Projects.Domain.Stage;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class DocumentProjectRepository(IDocumentCollection<Project> collection, IRelayService relayService,
    IIndexerService indexerService) : IProjectRepository
{

    public async Task<Result<Project>> Get(ProjectId id)
    {
        if (id == null)
            return Result.Failure<Project>("ProjectId cannot be null");

        try
        {
            var projectResult = await collection.FindByIdAsync(id.Value);

            if (projectResult.IsSuccess && projectResult.Value != null)
                return Result.Success(projectResult.Value);

            var result = await Result.Try(() => indexerService.GetProjectByIdAsync(id.Value));
            if (result.IsFailure)
                return Result.Failure<Project>(
                    $"Project with ID '{id.Value}' not found in indexer: {result.Error}");

            var projectInfo = await ProjectInfos(new[] { result.Value.NostrEventId });
            if (projectInfo.IsFailure || !projectInfo.Value.Any())
                return Result.Failure<Project>("Project info not found in relay");

            var metadataResult = await ProjectMetadatas(new[] { projectInfo.Value.First().NostrPubKey });
            if (metadataResult.IsFailure || !metadataResult.Value.Any())
                return Result.Failure<Project>("Project metadata not found in relay");

            var info = projectInfo.Value.First();
            var metadata = metadataResult.Value.First().NostrMetadata;

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

            var insertResult = await collection.InsertAsync(project);
            return insertResult.IsFailure
                ? Result.Failure<Project>($"Failed to cache project locally: {insertResult.Error}")
                : Result.Success(project);
        }
        catch (Exception ex)
        {
            return Result.Failure<Project>($"Error retrieving project {id.Value}: {ex.Message}");
        }
    }


    public async Task<Result<IEnumerable<Project>>> GetAll(params ProjectId[] ids)
    {
        if (ids == null || ids.Length == 0)
            return Result.Failure<IEnumerable<Project>>("ProjectId cannot be null");

        try
        {
            var stringIds = ids.Select(id => id.Value).ToArray();

            var projectResult = await collection.FindAsync(p => stringIds.Any(id => id == p.Id.Value));

            if (projectResult.IsSuccess && projectResult.Value != null)
                return Result.Success(projectResult.Value);

            var results = await stringIds.ToObservable()
                .Select(id => Result.Try(() => indexerService.GetProjectByIdAsync(id)))
                .Merge()
                .ToList()
                .ToTask();

            var result = results.Combine();

            if (result.IsFailure)
                return Result.Failure<IEnumerable<Project>>(result.Error);

            var nostrEventIds = result.Value.Where(r => r != null).Select(r => r!.NostrEventId).ToArray();
            var projectInfo = await ProjectInfos(nostrEventIds);
            if (projectInfo.IsFailure || !projectInfo.Value.Any())
                return Result.Failure<IEnumerable<Project>>("Project info not found in relay");

            var metadataResult = await ProjectMetadatas(new[] { projectInfo.Value.First().NostrPubKey });
            if (metadataResult.IsFailure || !metadataResult.Value.Any())
                return Result.Failure<IEnumerable<Project>>("Project metadata not found in relay");

            var lookupList = result.Value.Select(data =>
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
            });

            var response = lookupList.Where(p => p != null).Select(p => p!).ToList();

            if (!response.Any())
                return Result.Success(Array.Empty<Project>().AsEnumerable());

            var insertResult = await collection.InsertAsync(response.ToArray());

            return Result.Success(response.AsEnumerable());
        }
        catch (Exception ex)
        {
            return Result.Failure<IEnumerable<Project>>(ex.Message);
        }
    }

    public Task<Result<IEnumerable<Project>>> Latest()
        => collection.FindAllAsync()
            .Map(projects => projects.OrderByDescending(p => p.StartingDate).Take(30));
    
    
    private Uri? TryGetUri(string uriString)
    {
        return Uri.TryCreate(uriString, UriKind.Absolute, out var uri) ? uri : null;
    }
    
    private Task<Result<IEnumerable<ProjectInfo>>> ProjectInfos( IEnumerable<string> eventIds)
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