using System;
using System.IO;
using Angor.Contests.CrossCutting;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Microsoft.Extensions.Logging;

namespace Angor.Data.Documents.LiteDb;

public class LiteDbDocumentDatabaseFactory : IAngorDocumentDatabaseFactory
{
    private readonly ILogger<LiteDbDocumentDatabase> logger;
    private readonly IApplicationStorage storage;
    private readonly ProfileContext profileContext;
    private readonly INetworkStorage networkStorage;

    public LiteDbDocumentDatabaseFactory(
        ILogger<LiteDbDocumentDatabase> logger,
        IApplicationStorage storage,
        ProfileContext profileContext,
        INetworkStorage networkStorage)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.profileContext = profileContext ?? throw new ArgumentNullException(nameof(profileContext));
        this.networkStorage = networkStorage ?? throw new ArgumentNullException(nameof(networkStorage));

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
        var networkName = SanitizeNetworkName(networkStorage.GetNetwork());
        var networkDirectory = Path.Combine(profileDirectory, networkName);
        Directory.CreateDirectory(networkDirectory);
        var fileName = "angor-documents.db";
        var filePath = Path.Combine(networkDirectory, fileName);
        var connectionString = $"Filename={filePath}";

        //TODO generate a password and encrypt it with the DPAPI for the user profile
        // var password = GenerateSecurePasswordForProfile(profileName);
        // connectionString += $";Password={password}";

        logger.LogInformation("Creating LiteDB database for profile '{Profile}' on network '{Network}' at: {Path}",
            profileName, networkName, filePath);

        return new LiteDbDocumentDatabase(connectionString, logger);
    }

    private static string SanitizeNetworkName(string? network) =>
        string.IsNullOrWhiteSpace(network)
            ? "Angornet"
            : network.Replace(Path.DirectorySeparatorChar, '_')
                     .Replace(Path.AltDirectorySeparatorChar, '_');
}
