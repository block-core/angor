using System.Linq.Expressions;
using Angor.Data.Documents.Interfaces;
using Angor.Data.Documents.Models;
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

    // ... rest of the methods remain the same as previously implemented
    public async Task<string> InsertAsync(T document)
    {
        try
        {
            document.CreatedAt = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;
            
            var result = _collection.Insert(document);
            _logger.LogDebug("Inserted document {Type} with ID: {Id}", typeof(T).Name, result.ToString());
            
            return await Task.FromResult(result.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert document {Type}", typeof(T).Name);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(T document)
    {
        try
        {
            document.UpdatedAt = DateTime.UtcNow;
            var result = _collection.Update(document);
            
            _logger.LogDebug("Updated document {Type} with ID: {Id}, Success: {Success}", 
                typeof(T).Name, document.Id, result);
            
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update document {Type} with ID: {Id}", typeof(T).Name, document.Id);
            throw;
        }
    }

    public async Task<bool> UpsertAsync(T document)
    {
        try
        {
            document.UpdatedAt = DateTime.UtcNow;
            if (string.IsNullOrEmpty(document.Id) || !await ExistsAsync(document.Id))
            {
                document.CreatedAt = DateTime.UtcNow;
            }
            
            var result = _collection.Upsert(document);
            _logger.LogDebug("Upserted document {Type} with ID: {Id}, Success: {Success}", 
                typeof(T).Name, document.Id, result);
            
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert document {Type} with ID: {Id}", typeof(T).Name, document.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        try
        {
            var result = _collection.Delete(new BsonValue(id));
            _logger.LogDebug("Deleted document {Type} with ID: {Id}, Success: {Success}", 
                typeof(T).Name, id, result);
            
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {Type} with ID: {Id}", typeof(T).Name, id);
            throw;
        }
    }

    public async Task<T?> FindByIdAsync(string id)
    {
        try
        {
            var result = _collection.FindById(new BsonValue(id));
            _logger.LogDebug("Found document {Type} with ID: {Id}, Exists: {Exists}", 
                typeof(T).Name, id, result != null);
            
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find document {Type} with ID: {Id}", typeof(T).Name, id);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string id)
    {
        try
        {
            var exists = _collection.Exists(Query.EQ("_id", new BsonValue(id)));
            _logger.LogDebug("Document {Type} with ID: {Id} exists: {Exists}", 
                typeof(T).Name, id, exists);
            
            return await Task.FromResult(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence of document {Type} with ID: {Id}", typeof(T).Name, id);
            throw;
        }
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        try
        {
            var results = _collection.Find(predicate).ToList();
            _logger.LogDebug("Found {Count} documents of type {Type}", results.Count, typeof(T).Name);
            
            return await Task.FromResult(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find documents {Type}", typeof(T).Name);
            throw;
        }
    }

    public async Task<IEnumerable<T>> FindAllAsync()
    {
        try
        {
            var results = _collection.FindAll().ToList();
            _logger.LogDebug("Retrieved all {Count} documents of type {Type}", results.Count, typeof(T).Name);
            
            return await Task.FromResult(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all documents {Type}", typeof(T).Name);
            throw;
        }
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await FindAllAsync();
    }

    public async Task<int> CountAsync()
    {
        try
        {
            var count = _collection.Count();
            _logger.LogDebug("Counted {Count} documents of type {Type}", count, typeof(T).Name);
            
            return await Task.FromResult(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count documents {Type}", typeof(T).Name);
            throw;
        }
    }

    public async Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        try
        {
            var count = predicate != null ? _collection.Count(predicate) : _collection.Count();
            _logger.LogDebug("Counted {Count} documents of type {Type}", count, typeof(T).Name);
            
            return await Task.FromResult(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count documents {Type}", typeof(T).Name);
            throw;
        }
    }
}