using System;
using System.IO;
using Angor.Data.Documents.Interfaces;
using Angor.Shared.Utilities;
using Microsoft.Extensions.Logging;

namespace Angor.Data.Documents.LiteDb;

public class LiteDbDocumentDatabaseFactory : IAngorDocumentDatabaseFactory
{
    private readonly ILogger<LiteDbDocumentDatabase> logger;
    private readonly string appName;

    public LiteDbDocumentDatabaseFactory(ILogger<LiteDbDocumentDatabase> logger, string profileName, string appName = "Angor")
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(appName))
        {
            throw new ArgumentException("App name cannot be null or whitespace.", nameof(appName));
        }

        this.appName = appName;

        var profileDirectory = ApplicationStoragePaths.GetProfileDirectory(appName, profileName);

        if (profileDirectory.IsFailure)
        {
            throw new ArgumentException(profileDirectory.Error, nameof(profileName));
        }
    }

    public IAngorDocumentDatabase CreateDatabase(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or whitespace.", nameof(profileName));
        }

        var profileDirectory = ApplicationStoragePaths.GetProfileDirectory(appName, profileName);

        if (profileDirectory.IsFailure)
        {
            throw new ArgumentException(profileDirectory.Error, nameof(profileName));
        }

        var sanitizedProfile = ApplicationStoragePaths.SanitizeProfileName(profileName);
        var fileName = $"angor-documents-{sanitizedProfile}.db";
        var filePath = Path.Combine(profileDirectory.Value, fileName);
        var connectionString = $"Filename={filePath}";

        //TODO generate a password and encrypt it with the DPAPI for the user profile
        // var password = GenerateSecurePasswordForProfile(profileName);
        // connectionString += $";Password={password}";

        logger.LogInformation("Creating LiteDB database for profile '{Profile}' at: {Path}",
            profileName, filePath);

        return new LiteDbDocumentDatabase(connectionString, logger);
    }
}