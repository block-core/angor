using System.Collections.Concurrent;
using Angor.Data.Documents.Interfaces;
using Angor.Data.Documents.Models;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace Angor.Data.Documents.LiteDb;

public class LiteDbDocumentDatabase : IAngorDocumentDatabase
{
    private readonly LiteDatabase _database;
    private readonly ILogger<LiteDbDocumentDatabase> _logger;
    private readonly string _databasePath;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _collectionLocks = new();

    public LiteDbDocumentDatabase(string connectionString, ILogger<LiteDbDocumentDatabase> logger)
    {
        _logger = logger;
        _databasePath = ExtractFilePathFromConnectionString(connectionString);
        
        // Force LiteDB to store and retrieve all DateTime values in UTC.
        // LiteDB 5.x stores DateTime as BSON DateTime (UTC milliseconds)
        // but on deserialization may return DateTimeKind.Local, silently
        // converting UTC dates to local time.  When those dates are later
        // used to rebuild Bitcoin taproot scripts (CLTV locktimes, expiry
        // dates), the different Kind causes .Date to return a different
        // calendar day in non-UTC timezones, producing a different script
        // hash and "Witness program hash mismatch".
        BsonMapper.Global.RegisterType<DateTime>(
            serialize: dt => new BsonValue(dt.ToUniversalTime()),
            deserialize: bson => bson.AsDateTime.ToUniversalTime()
        );

        _database = OpenDatabaseWithRecovery(connectionString);
    }

    public IDocumentCollection<T> GetCollection<T>() where T : BaseDocument
    {
        var semaphore = _collectionLocks.GetOrAdd(typeof(T).FullName!, _ => new SemaphoreSlim(1, 1));
        return new LiteDbDocumentCollection<T>(_database, _logger, semaphore);
    }

    public IDocumentCollection<T> GetCollection<T>(string collectionName) where T : BaseDocument
    {
        if (string.IsNullOrEmpty(collectionName))
            return GetCollection<T>();

        var semaphore = _collectionLocks.GetOrAdd(collectionName, _ => new SemaphoreSlim(1, 1));
        return new LiteDbDocumentCollection<T>(_database, _logger, semaphore, collectionName);
    }

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
            _database.Checkpoint();
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

    public async Task<bool> CheckpointAsync()
    {
        try
        {
            _database.Checkpoint();
            _logger.LogDebug("Database checkpoint performed");
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform database checkpoint");
            return false;
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
        _database.Checkpoint();
        _database.Dispose();
        _logger.LogInformation("LiteDB database disposed");
    }

    /// <summary>
    /// Opens the LiteDB database. If the initial open fails (e.g. corrupted WAL from
    /// an unclean Android shutdown), deletes the WAL journal file and retries.
    /// This preserves the main data file while discarding uncommitted changes.
    /// </summary>
    private LiteDatabase OpenDatabaseWithRecovery(string connectionString)
    {
        try
        {
            var db = new LiteDatabase(connectionString);
            _logger.LogInformation("Initialized LiteDB database: {Path}", _databasePath);
            return db;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LiteDB failed to open, attempting WAL recovery: {Path}", _databasePath);

            try
            {
                // LiteDB 5.x WAL file uses the "-log" suffix
                var walPath = _databasePath + "-log";
                if (File.Exists(walPath))
                {
                    File.Delete(walPath);
                    _logger.LogInformation("Deleted corrupted WAL file: {WalPath}", walPath);
                }

                var db = new LiteDatabase(connectionString);
                _logger.LogInformation("LiteDB opened successfully after WAL recovery: {Path}", _databasePath);
                return db;
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "LiteDB failed to open even after WAL recovery: {Path}", _databasePath);
                throw;
            }
        }
    }
}