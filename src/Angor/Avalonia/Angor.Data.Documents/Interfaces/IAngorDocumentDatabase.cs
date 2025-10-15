namespace Angor.Data.Documents.Interfaces;

public interface IAngorDocumentDatabase : IDisposable
{
    IDocumentCollection<T> GetCollection<T>(string? name = null) where T : class;
    Task<bool> BeginTransactionAsync();
    Task<bool> CommitAsync();
    Task<bool> RollbackAsync();
    Task<bool> DatabaseExistsAsync();
    Task<long> GetDatabaseSizeAsync();
}