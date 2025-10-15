using Angor.Data.Documents.Interfaces;
using Angor.Data.Documents.Models;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace Angor.Data.Documents.LiteDb;

public class LiteDbDocumentDatabase : IAngorDocumentDatabase, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILogger<LiteDbDocumentDatabase> _logger;
    private readonly string _databasePath;

    public LiteDbDocumentDatabase(string connectionString, ILogger<LiteDbDocumentDatabase> logger)
    {
        _logger = logger;
        _databasePath = ExtractFilePathFromConnectionString(connectionString);
        
        try
        {
            _database = new LiteDatabase(connectionString);
            _logger.LogInformation("Initialized LiteDB database: {ConnectionString}", connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize LiteDB database: {ConnectionString}", connectionString);
            throw;
        }
    }

    public IDocumentCollection<T> GetCollection<T>() where T : BaseDocument
    {
        // Create a new wrapper each time - it's lightweight
        return new LiteDbDocumentCollection<T>(_database, _logger);
    }

    public IDocumentCollection<T> GetCollection<T>(string? collectionName = null) where T : BaseDocument
    {
        // If no custom name provided, use the standard approach
        if (string.IsNullOrEmpty(collectionName))
        {
            return GetCollection<T>();
        }
        
        // Only create custom-named collection if explicitly requested
        return new LiteDbDocumentCollection<T>(_database, _logger, collectionName);
    }

    // ... rest of the methods remain the same
    public async Task<bool> BeginTransactionAsync()
    {
        try
        {
            _database.BeginTrans();
            _logger.LogDebug("Transaction started");
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start transaction");
            return false;
        }
    }

    public async Task<bool> CommitAsync()
    {
        try
        {
            _database.Commit();
            _logger.LogDebug("Transaction committed");
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit transaction");
            return false;
        }
    }

    public async Task<bool> CommitTransactionAsync() => await CommitAsync();

    public async Task<bool> RollbackAsync()
    {
        try
        {
            _database.Rollback();
            _logger.LogDebug("Transaction rolled back");
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback transaction");
            return false;
        }
    }

    public async Task<bool> RollbackTransactionAsync() => await RollbackAsync();

    public async Task<bool> DatabaseExistsAsync()
    {
        try
        {
            var exists = File.Exists(_databasePath);
            _logger.LogDebug("Database exists: {Exists} at path: {Path}", exists, _databasePath);
            return await Task.FromResult(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if database exists at: {Path}", _databasePath);
            return false;
        }
    }

    public async Task<long> GetDatabaseSizeAsync()
    {
        try
        {
            if (!File.Exists(_databasePath))
            {
                return await Task.FromResult(0L);
            }

            var fileInfo = new FileInfo(_databasePath);
            var size = fileInfo.Length;
            
            _logger.LogDebug("Database size: {Size} bytes at path: {Path}", size, _databasePath);
            return await Task.FromResult(size);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get database size at: {Path}", _databasePath);
            return 0L;
        }
    }

    private static string ExtractFilePathFromConnectionString(string connectionString)
    {
        const string filenamePrefix = "Filename=";
        if (connectionString.StartsWith(filenamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return connectionString.Substring(filenamePrefix.Length);
        }
        
        return connectionString;
    }

    public void Dispose()
    {
        _database?.Dispose();
        _logger.LogInformation("LiteDB database disposed");
    }
}