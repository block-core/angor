using Angor.Data.Documents.Interfaces;
using CSharpFunctionalExtensions;

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
        return await _collection.UpsertAsync(d => d.WalletId, draft);
    }

    public async Task<Result<ProjectDraft?>> LoadAsync(string walletId)
    {
        return await _collection.FindByIdAsync(walletId);
    }

    public async Task<Result> DeleteAsync(string walletId)
    {
        return await _collection.DeleteAsync(walletId);
    }
}
