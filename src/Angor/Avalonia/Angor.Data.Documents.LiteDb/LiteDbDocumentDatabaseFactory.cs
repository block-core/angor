using Angor.Data.Documents.Interfaces;
using Microsoft.Extensions.Logging;

namespace Angor.Data.Documents.LiteDb;

public class LiteDbDocumentDatabaseFactory : IAngorDocumentDatabaseFactory
{
    private readonly ILogger<LiteDbDocumentDatabase> _logger;
    private readonly string _basePath;

    public LiteDbDocumentDatabaseFactory(ILogger<LiteDbDocumentDatabase> logger, string profileName, string appName = "Angor")
    {
        _logger = logger;
        
        _basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appName,
            profileName
        );
        
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

        //TODO generate a password and encrypt it with the DPAPI for the user profile
        // var password = GenerateSecurePasswordForProfile(profileName);
        // connectionString += $";Password={password}"; 
        
        _logger.LogInformation("Creating LiteDB database for profile '{Profile}' at: {Path}", 
            profileName, filePath);

        return new LiteDbDocumentDatabase(connectionString, _logger);
    }
}