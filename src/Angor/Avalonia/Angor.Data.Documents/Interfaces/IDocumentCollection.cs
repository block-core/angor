using System.Linq.Expressions;

namespace Angor.Data.Documents.Interfaces;

public interface IDocumentCollection<T> where T : class
{
    Task<T?> FindByIdAsync(string id);
    Task<IEnumerable<T>> FindAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<string> InsertAsync(T document);
    Task<bool> UpdateAsync(T document);
    Task<bool> DeleteAsync(string id);
    Task<int> CountAsync();
    Task<bool> ExistsAsync(string id);
    Task<bool> UpsertAsync(T document);
}