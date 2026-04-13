using Angor.Data.Documents.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Founder.Domain;

/// <summary>
/// Local-only persistence for founder projects.
/// Stores which projects each wallet has created so that
/// <see cref="Operations.GetFounderProjects"/> can load them
/// without scanning all 15 derived keys via the indexer.
/// </summary>
public class FounderProjectsService(
    IGenericDocumentCollection<FounderProjectsDocument> documentCollection) : IFounderProjectsService
{
    public async Task<Result<List<FounderProjectRecord>>> GetByWalletId(string walletId)
    {
        var result = await documentCollection.FindByIdAsync(walletId);

        if (result is { IsSuccess: true, Value: not null })
            return Result.Success(result.Value.Projects);

        // No document yet — return empty list (not a failure)
        return Result.Success(new List<FounderProjectRecord>());
    }

    public async Task<Result> Add(string walletId, FounderProjectRecord project)
    {
        return await AddRange(walletId, [project]);
    }

    public async Task<Result> AddRange(string walletId, IEnumerable<FounderProjectRecord> projects)
    {
        var existing = await GetByWalletId(walletId);
        var list = existing.IsSuccess ? existing.Value : new List<FounderProjectRecord>();

        var existingIds = new HashSet<string>(list.Select(p => p.ProjectIdentifier));

        foreach (var project in projects)
        {
            if (!existingIds.Contains(project.ProjectIdentifier))
            {
                list.Add(project);
                existingIds.Add(project.ProjectIdentifier);
            }
        }

        var doc = new FounderProjectsDocument
        {
            WalletId = walletId,
            Projects = list
        };

        var saved = await documentCollection.UpsertAsync(d => d.WalletId, doc);
        return saved.IsSuccess
            ? Result.Success()
            : Result.Failure("Failed to persist founder projects locally.");
    }
}
