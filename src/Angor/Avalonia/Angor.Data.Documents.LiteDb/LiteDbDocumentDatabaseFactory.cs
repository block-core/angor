using Angor.Data.Documents.Interfaces;
using Microsoft.Extensions.Logging;

namespace Angor.Data.Documents.LiteDb;

public class LiteDbDocumentDatabaseFactory : IAngorDocumentDatabaseFactory
{
    private readonly ILogger<LiteDbDocumentDatabase> _logger;
    private readonly string _basePath;

    public LiteDbDocumentDatabaseFactory(ILogger<LiteDbDocumentDatabase> logger)
    {
        _logger = logger;
        
        // Use the same pattern as your existing FileStore
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _basePath = Path.Combine(appDataPath, "Angor");
        
        // Ensure directory exists
        Directory.CreateDirectory(_basePath);
    }

    public IAngorDocumentDatabase CreateDatabase(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or whitespace.", nameof(profileName));
        }

        var fileName = $"angor-documents-{profileName}.db";
        var filePath = Path.Combine(_basePath, fileName);
        var connectionString = $"Filename={filePath}";

        _logger.LogInformation("Creating LiteDB database for profile '{Profile}' at: {Path}", 
            profileName, filePath);

        return new LiteDbDocumentDatabase(connectionString, _logger);
    }
}