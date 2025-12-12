using System;
using System.IO;
using System.Linq;
using Angor.Sdk.Common;

namespace AngorApp.Core;

public class ApplicationStorage : IApplicationStorage
{
    private const string ProfilesFolderName = "Profiles";
    private const string LogsFolderName = "Logs";

    public string GetRoot(string appName)
    {
        var normalizedName = NormalizeName(appName, nameof(appName));
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), normalizedName);
        return EnsureDirectory(root);
    }

    public string GetProfilesRoot(string appName)
    {
        var root = GetRoot(appName);
        return EnsureDirectory(Path.Combine(root, ProfilesFolderName));
    }

    public string GetLogsDirectory(string appName)
    {
        var root = GetRoot(appName);
        return EnsureDirectory(Path.Combine(root, LogsFolderName));
    }

    public string GetProfileDirectory(string appName, string profileName)
    {
        var verifiedProfile = EnsureProfileName(profileName);
        var profilesRoot = GetProfilesRoot(appName);
        return EnsureDirectory(Path.Combine(profilesRoot, verifiedProfile));
    }

    public string GetProfileFilePath(string appName, string profileName, string fileName)
    {
        var normalizedFileName = NormalizeName(fileName, nameof(fileName));
        var profileDirectory = GetProfileDirectory(appName, profileName);
        return Path.Combine(profileDirectory, normalizedFileName);
    }

    public string SanitizeProfileName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return "Default";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var trimmed = profileName.Trim();
        var sanitized = new string(trimmed
            .Select(ch => invalidChars.Contains(ch) ? '-' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "Default" : sanitized;
    }

    private static string NormalizeName(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{argumentName} cannot be null or whitespace.", argumentName);
        }

        return value.Trim();
    }

    private static string EnsureProfileName(string profileName)
    {
        var trimmed = NormalizeName(profileName, nameof(profileName));
        var invalidChars = Path.GetInvalidFileNameChars();

        if (trimmed.Any(ch => invalidChars.Contains(ch)))
        {
            throw new ArgumentException("Profile name contains invalid characters. Sanitize before calling.", nameof(profileName));
        }

        return trimmed;
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
