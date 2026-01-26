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
}