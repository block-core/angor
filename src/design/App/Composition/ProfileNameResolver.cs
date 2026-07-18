using System;

namespace App.Composition;

public static class ProfileNameResolver
{
    public const string DefaultProfileName = "Default";
    public const string ProfileOption = "--profile";
    public const string ProfileEnvVar = "ANGOR_PROFILE";

    public static string GetProfileName(string[]? args)
    {
        var fromArgs = GetProfileNameFromArgs(args);
        if (fromArgs != null)
            return fromArgs;

        // Fallback for platforms without command-line args (Android): the
        // ANGOR_PROFILE env var is populated from debug.angor.profile via
        // DebugTestConfig before the DI container is built.
        var fromEnv = Environment.GetEnvironmentVariable(ProfileEnvVar)?.Trim();
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        return DefaultProfileName;
    }

    private static string? GetProfileNameFromArgs(string[]? args)
    {
        if (args == null || args.Length == 0)
            return null;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.IsNullOrWhiteSpace(argument))
                continue;

            if (argument.StartsWith(ProfileOption + "=", StringComparison.OrdinalIgnoreCase))
            {
                var value = argument.Substring(ProfileOption.Length + 1).Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            if (!string.Equals(argument, ProfileOption, StringComparison.OrdinalIgnoreCase))
                continue;

            if (index + 1 < args.Length)
            {
                var value = args[index + 1]?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("--", StringComparison.Ordinal))
                    return value;
            }

            return null;
        }

        return null;
    }
}
