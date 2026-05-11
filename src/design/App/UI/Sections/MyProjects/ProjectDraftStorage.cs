using Angor.Data.Documents.Interfaces;
using Angor.Primitives;

namespace App.UI.Sections.MyProjects;

/// <summary>
/// Persists and retrieves project wizard drafts, keyed by wallet ID.
/// </summary>
public class ProjectDraftStorage
{
    private readonly IGenericDocumentCollection<ProjectDraft> _collection;

    public ProjectDraftStorage(IGenericDocumentCollection<ProjectDraft> collection)
    {
        _collection = collection;
    }

    public async Task<Result> SaveAsync(ProjectDraft draft)
    {
        draft.UpdatedAt = DateTime.UtcNow;
        var result = await _collection.UpsertAsync(d => d.WalletId, draft);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }

    public async Task<Result<ProjectDraft?>> LoadAsync(string walletId)
    {
        return await _collection.FindByIdAsync(walletId);
    }

    public async Task<Result> DeleteAsync(string walletId)
    {
        var result = await _collection.DeleteAsync(walletId);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }
}
