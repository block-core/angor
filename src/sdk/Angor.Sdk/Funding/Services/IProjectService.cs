using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Services;

public interface IProjectService
{
    Task<Result<Project>> GetAsync(ProjectId id);
    Task<Result<Maybe<Project>>> TryGetAsync(ProjectId projectId);
    Task<Result<IEnumerable<Project>>> GetAllAsync(params ProjectId[] ids);
    Task<Result<IEnumerable<Project>>> LatestAsync();
    
    /// <summary>
    /// Retrieves the latest projects by first querying Nostr relays for kind 3030 events,
    /// then validating each project exists on-chain via the indexer before returning full project data.
    /// Projects not found on-chain are filtered out (potential spam).
    /// </summary>
    Task<Result<IEnumerable<Project>>> LatestFromNostrAsync();

    /// <summary>
    /// Refresh a project's cached profile metadata (name/banner/picture/about) from
    /// relays in the background. By default the refresh only runs when the cached
    /// metadata is older than the freshness TTL; pass <paramref name="force"/> to
    /// bypass the TTL (e.g. right after the founder publishes a profile update).
    /// Returns immediately; the refresh completes in the background.
    /// </summary>
    Task<Result> RevalidateAsync(ProjectId id, bool force = false);
}