using CSharpFunctionalExtensions;

namespace Angor.Data.Documents.Interfaces;

using System.Linq.Expressions;

public interface IGenericDocumentCollection<T> where T : IDocumentEntity
{
    Task<Result<T?>> FindByIdAsync(string id);
    Task<Result<IEnumerable<T>>> FindAllAsync();
    Task<Result<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<Result<bool>> ExistsAsync(string id);
    Task<Result<int>> InsertAsync(params T[] entities);
    Task<Result<bool>> UpdateAsync(T entity);
    Task<Result<bool>> UpsertAsync(T entity);
    Task<Result<bool>> DeleteAsync(string id);
    Task<Result<int>> CountAsync(Expression<Func<T, bool>>? predicate = null);
}