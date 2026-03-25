using System.IO;
using Angor.Sdk.Common;

namespace App.Composition.Adapters;

/// <summary>
/// Application storage implementation for App.
/// Mirrors AngorApp's ApplicationStorage but uses "App" as the app name.
/// </summary>
public class AppApplicationStorage : IApplicationStorage
{
    public string GetRoot(string appName)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var path = Path.Combine(localAppData, NormalizeName(appName));
        EnsureDirectory(path);
        return path;
    }

    public string GetProfilesRoot(string appName)
    {
        var path = Path.Combine(GetRoot(appName), "Profiles");
        EnsureDirectory(path);
        return path;
    }

    public string GetLogsDirectory(string appName)
    {
        var path = Path.Combine(GetRoot(appName), "Logs");
        EnsureDirectory(path);
        return path;
    }

    public string GetProfileDirectory(string appName, string profileName)
    {
        var safe = EnsureProfileName(profileName);
        var path = Path.Combine(GetProfilesRoot(appName), safe);
        EnsureDirectory(path);
        return path;
    }

    public string GetProfileFilePath(string appName, string profileName, string fileName)
    {
        return Path.Combine(GetProfileDirectory(appName, profileName), fileName);
    }

    public string SanitizeProfileName(string profileName)
    {
        return EnsureProfileName(profileName);
    }

    private static string NormalizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    private static string EnsureProfileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Default";
        return NormalizeName(name);
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
