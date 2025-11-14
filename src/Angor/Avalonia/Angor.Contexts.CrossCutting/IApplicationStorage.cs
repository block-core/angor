namespace Angor.Contexts.CrossCutting;

public interface IApplicationStorage
{
    string GetRoot(string appName);

    string GetProfilesRoot(string appName);

    string GetLogsDirectory(string appName);

    string GetProfileDirectory(string appName, string profileName);

    string GetProfileFilePath(string appName, string profileName, string fileName);

    string SanitizeProfileName(string profileName);
}
