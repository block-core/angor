using System.Linq.Expressions;
using Angor.Data.Documents.Models;
using CSharpFunctionalExtensions;

namespace Angor.Data.Documents.Interfaces;

public interface IDocumentCollection<T> where T : BaseDocument
{
    // Read operations
    Task<Result<T?>> FindByIdAsync(string id);
    Task<Result<IEnumerable<T>>> FindAllAsync();
    Task<Result<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<Result<bool>> ExistsAsync(string id);
    Task<Result<IEnumerable<T>>> GetAllAsync();
    Task<Result<int>> CountAsync();
    Task<Result<long>> CountAsync(Expression<Func<T, bool>>? predicate = null);
    
    // Write operations
    Task<Result<int>> InsertAsync(params T[] document);
    Task<Result<bool>> UpdateAsync(T document);
    Task<Result<bool>> UpsertAsync(T document);
    Task<Result<bool>> DeleteAsync(string id);
}