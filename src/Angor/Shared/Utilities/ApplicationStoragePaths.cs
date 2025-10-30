using CSharpFunctionalExtensions;

namespace Angor.Shared.Utilities;

public static class ApplicationStoragePaths
{
    private const string ProfilesFolderName = "Profiles";
    private const string LogsFolderName = "Logs";

    public static Result<string> GetRoot(string appName)
    {
        return Result
            .Success(appName)
            .Ensure(name => !string.IsNullOrWhiteSpace(name), "App name cannot be null or whitespace.")
            .Map(name => name.Trim())
            .Bind(name => Result.Try(() =>
            {
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name);
                return EnsureDirectory(root);
            }));
    }

    public static Result<string> GetProfilesRoot(string appName)
    {
        return GetRoot(appName)
            .Map(root => EnsureDirectory(Path.Combine(root, ProfilesFolderName)));
    }

    public static Result<string> GetLogsDirectory(string appName)
    {
        return GetRoot(appName)
            .Map(root => EnsureDirectory(Path.Combine(root, LogsFolderName)));
    }

    public static Result<string> GetProfileDirectory(string appName, string profileName)
    {
        return Result
            .Success(profileName)
            .Ensure(name => !string.IsNullOrWhiteSpace(name), "Profile name cannot be null or whitespace.")
            .Map(SanitizeProfileName)
            .Bind(safeName => GetProfilesRoot(appName)
                .Map(root => EnsureDirectory(Path.Combine(root, safeName))));
    }

    public static Result<string> GetProfileFilePath(string appName, string profileName, string fileName)
    {
        return Result
            .Success(fileName)
            .Ensure(name => !string.IsNullOrWhiteSpace(name), "File name cannot be null or whitespace.")
            .Map(name => name.Trim())
            .Bind(name => GetProfileDirectory(appName, profileName)
                .Map(directory => Path.Combine(directory, name)));
    }

    public static string SanitizeProfileName(string profileName)
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

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
