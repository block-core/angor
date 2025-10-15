using Angor.Data.Documents.Models;

namespace Angor.Data.Documents.Interfaces;

public interface IAngorDocumentDatabase
public interface IAngorDocumentDatabase : IDisposable
{
    IDocumentCollection<T> GetCollection<T>() where T : BaseDocument;
    IDocumentCollection<T> GetCollection<T>(string name) where T : BaseDocument;
    
    Task<bool> BeginTransactionAsync();
    Task<bool> CommitAsync();
    Task<bool> CommitTransactionAsync();
    Task<bool> RollbackAsync();
    Task<bool> RollbackTransactionAsync();
    Task<bool> DatabaseExistsAsync();
    Task<long> GetDatabaseSizeAsync();
}