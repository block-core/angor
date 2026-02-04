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
    /// Fetches the relay list from a user's Nostr account using NIP-65.
    /// Returns the list of relay URLs published by the user, or an empty list if none found.
    /// </summary>
    /// <param name="nostrPubKey">The Nostr public key (hex format) to lookup relays for</param>
    /// <returns>A list of relay URLs</returns>
    Task<Result<IEnumerable<string>>> GetRelaysForNpubAsync(string nostrPubKey);
}