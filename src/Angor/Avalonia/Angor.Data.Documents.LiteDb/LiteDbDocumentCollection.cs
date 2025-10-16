using System.Linq.Expressions;
using Angor.Data.Documents.Interfaces;
using Angor.Data.Documents.Models;
using CSharpFunctionalExtensions;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace Angor.Data.Documents.LiteDb;

public class LiteDbDocumentCollection<T> : IDocumentCollection<T> where T : BaseDocument
{
    private readonly ILiteCollection<T> _collection;
    private readonly ILogger _logger;

    public LiteDbDocumentCollection(LiteDatabase database, ILogger logger, string? collectionName = null)
    {
        _logger = logger;
        
        // Use custom collection name if provided, otherwise use the type name
        var name = collectionName ?? typeof(T).Name;
        _collection = database.GetCollection<T>(name);
        
        // Ensure indexes on common fields
        _collection.EnsureIndex(x => x.Id);
        _collection.EnsureIndex(x => x.CreatedAt);
        _collection.EnsureIndex(x => x.UpdatedAt);
    }

    public async Task<Result<int>> InsertAsync(params T[] documents)
    {
        try
        {
            if (documents.Length == 0)
                return Result.Failure<int>("Document cannot be null");

            foreach (var document in documents)
            {
                document.CreatedAt = DateTime.UtcNow;
                document.UpdatedAt = DateTime.UtcNow;
            }

            var result = _collection.Insert(documents);

            if (result != documents.Length)
            {
                _logger.LogWarning("Expected to insert {expected} documents but only inserted {actual}", 
                    documents.Length, result);
                
                return Result.Success(result);
            }
            
            _logger.LogDebug("Inserted {total} document {Type}}",result, typeof(T).Name);
            
            return await Task.FromResult(Result.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert document {Type}", typeof(T).Name);
            return Result.Failure<int>($"Failed to insert document: {ex.Message}");
        }
    }

    public async Task<Result<bool>> UpdateAsync(T document)
    {
        try
        {
            if (document == null)
                return Result.Failure<bool>("Document cannot be null");

            if (string.IsNullOrEmpty(document.Id))
                return Result.Failure<bool>("Document ID cannot be null or empty");

            document.UpdatedAt = DateTime.UtcNow;
            var result = _collection.Update(document);
            
            _logger.LogDebug("Updated document {Type} with ID: {Id}, Success: {Success}", 
                typeof(T).Name, document.Id, result);
            
            return await Task.FromResult(Result.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update document {Type} with ID: {Id}", typeof(T).Name, document?.Id);
            return Result.Failure<bool>($"Failed to update document: {ex.Message}");
        }
    }

    public async Task<Result<bool>> UpsertAsync(T document)
    {
        try
        {
            if (document == null)
                return Result.Failure<bool>("Document cannot be null");

            document.UpdatedAt = DateTime.UtcNow;
            
            if (string.IsNullOrEmpty(document.Id))
            {
                document.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                var existsResult = await ExistsAsync(document.Id);
                if (existsResult.IsFailure)
                    return Result.Failure<bool>($"Failed to check document existence: {existsResult.Error}");
                
                if (!existsResult.Value)
                    document.CreatedAt = DateTime.UtcNow;
            }
            
            var result = _collection.Upsert(document);
            _logger.LogDebug("Upserted document {Type} with ID: {Id}, Success: {Success}", 
                typeof(T).Name, document.Id, result);
            
            return await Task.FromResult(Result.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert document {Type} with ID: {Id}", typeof(T).Name, document?.Id);
            return Result.Failure<bool>($"Failed to upsert document: {ex.Message}");
        }
    }

    public async Task<Result<bool>> DeleteAsync(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id))
                return Result.Failure<bool>("ID cannot be null or empty");

            var result = _collection.Delete(new BsonValue(id));
            _logger.LogDebug("Deleted document {Type} with ID: {Id}, Success: {Success}", 
                typeof(T).Name, id, result);
            
            return await Task.FromResult(Result.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {Type} with ID: {Id}", typeof(T).Name, id);
            return Result.Failure<bool>($"Failed to delete document: {ex.Message}");
        }
    }

    public async Task<Result<T?>> FindByIdAsync(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id))
                return Result.Failure<T?>("ID cannot be null or empty");

            var result = _collection.FindById(new BsonValue(id));
            _logger.LogDebug("Found document {Type} with ID: {Id}, Exists: {Exists}", 
                typeof(T).Name, id, result != null);
            
            return await Task.FromResult(Result.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find document {Type} with ID: {Id}", typeof(T).Name, id);
            return Result.Failure<T?>($"Failed to find document: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ExistsAsync(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id))
                return Result.Failure<bool>("ID cannot be null or empty");

            var exists = _collection.Exists(Query.EQ("_id", new BsonValue(id)));
            _logger.LogDebug("Document {Type} with ID: {Id} exists: {Exists}", 
                typeof(T).Name, id, exists);
            
            return await Task.FromResult(Result.Success(exists));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence of document {Type} with ID: {Id}", typeof(T).Name, id);
            return Result.Failure<bool>($"Failed to check document existence: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        try
        {
            if (predicate == null)
                return Result.Failure<IEnumerable<T>>("Predicate cannot be null");

            var results = _collection.Find(predicate).ToList();
            _logger.LogDebug("Found {Count} documents of type {Type}", results.Count, typeof(T).Name);
            
            return await Task.FromResult(Result.Success<IEnumerable<T>>(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find documents of type {Type}", typeof(T).Name);
            return Result.Failure<IEnumerable<T>>($"Failed to find documents: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<T>>> FindAllAsync()
    {
        try
        {
            var results = _collection.FindAll().ToList();
            _logger.LogDebug("Found {Count} documents of type {Type}", results.Count, typeof(T).Name);
            
            return await Task.FromResult(Result.Success<IEnumerable<T>>(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find all documents of type {Type}", typeof(T).Name);
            return Result.Failure<IEnumerable<T>>($"Failed to find all documents: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<T>>> GetAllAsync()
    {
        return await FindAllAsync();
    }

    public async Task<Result<int>> CountAsync()
    {
        try
        {
            var count = _collection.Count();
            _logger.LogDebug("Count of documents {Type}: {Count}", typeof(T).Name, count);
            
            return await Task.FromResult(Result.Success(count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count documents of type {Type}", typeof(T).Name);
            return Result.Failure<int>($"Failed to count documents: {ex.Message}");
        }
    }

    public async Task<Result<long>> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        try
        {
            var count = predicate != null 
                ? _collection.Count(predicate) 
                : _collection.LongCount();
                
            _logger.LogDebug("Count of documents {Type} with predicate: {Count}", typeof(T).Name, count);
            
            return await Task.FromResult(Result.Success(count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count documents of type {Type} with predicate", typeof(T).Name);
            return Result.Failure<long>($"Failed to count documents: {ex.Message}");
        }
    }
}