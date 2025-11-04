using System;
using System.IO;
using Angor.Contests.CrossCutting;
using Angor.Data.Documents.Interfaces;
using Microsoft.Extensions.Logging;

namespace Angor.Data.Documents.LiteDb;

public class LiteDbDocumentDatabaseFactory : IAngorDocumentDatabaseFactory
{
    private readonly ILogger<LiteDbDocumentDatabase> logger;
    private readonly IApplicationStorage storage;
    private readonly ProfileContext profileContext;

    public LiteDbDocumentDatabaseFactory(
        ILogger<LiteDbDocumentDatabase> logger,
        IApplicationStorage storage,
        ProfileContext profileContext)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.profileContext = profileContext ?? throw new ArgumentNullException(nameof(profileContext));

        _ = this.storage.GetProfileDirectory(profileContext.AppName, profileContext.ProfileName);
    }

    public IAngorDocumentDatabase CreateDatabase(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or whitespace.", nameof(profileName));
        }

        if (!string.Equals(profileName, profileContext.ProfileName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Profile name '{profileName}' does not match the configured profile '{profileContext.ProfileName}'. Ensure the profile is sanitized at input.",
                nameof(profileName));
        }

        var profileDirectory = storage.GetProfileDirectory(profileContext.AppName, profileContext.ProfileName);
        var fileName = $"angor-documents-{profileContext.ProfileName}.db";
        var filePath = Path.Combine(profileDirectory, fileName);
        var connectionString = $"Filename={filePath}";

        //TODO generate a password and encrypt it with the DPAPI for the user profile
        // var password = GenerateSecurePasswordForProfile(profileName);
        // connectionString += $";Password={password}";

        logger.LogInformation("Creating LiteDB database for profile '{Profile}' at: {Path}",
            profileName, filePath);

        return new LiteDbDocumentDatabase(connectionString, logger);
    }
}
