using CSharpFunctionalExtensions;

namespace Angor.Data.Documents.Interfaces;

using System.Linq.Expressions;

public interface IGenericDocumentCollection<T> where T : class
{
    Task<Result<T?>> FindByIdAsync(string id);
    Task<Result<IEnumerable<T>>> FindByIdsAsync(IEnumerable<string> ids);
    Task<Result<IEnumerable<T>>> FindAllAsync();
    Task<Result<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<Result<bool>> ExistsAsync(string id);
    Task<Result<int>> InsertAsync(Expression<Func<T,string>> getDocumentId,params T[] entities);
    Task<Result<bool>> UpdateAsync(Expression<Func<T,string>> getDocumentId,T entity);
    Task<Result<bool>> UpsertAsync(Expression<Func<T,string>> getDocumentId,T entity);
    Task<Result<bool>> DeleteAsync(string id);
    Task<Result<int>> CountAsync(Expression<Func<T, bool>>? predicate = null);
}