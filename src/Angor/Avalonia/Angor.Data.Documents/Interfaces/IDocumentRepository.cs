using System.Linq.Expressions;

namespace Angor.Data.Documents.Interfaces;

public interface IDocumentRepository<T> where T : class
{
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate);
    Task<string> InsertAsync(T document);
    Task<bool> UpdateAsync(T document);
    Task<bool> DeleteAsync(string id);
    Task<int> CountAsync();
    Task<bool> ExistsAsync(string id);
    Task<bool> UpsertAsync(T document);
}