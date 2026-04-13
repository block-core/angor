using System;

namespace App.Composition;

public static class ProfileNameResolver
{
    public const string DefaultProfileName = "Default";
    public const string ProfileOption = "--profile";

    public static string GetProfileName(string[]? args)
    {
        if (args == null || args.Length == 0)
            return DefaultProfileName;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.IsNullOrWhiteSpace(argument))
                continue;

            if (argument.StartsWith(ProfileOption + "=", StringComparison.OrdinalIgnoreCase))
            {
                var value = argument.Substring(ProfileOption.Length + 1).Trim();
                return string.IsNullOrWhiteSpace(value) ? DefaultProfileName : value;
            }

            if (!string.Equals(argument, ProfileOption, StringComparison.OrdinalIgnoreCase))
                continue;

            if (index + 1 < args.Length)
            {
                var value = args[index + 1]?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("--", StringComparison.Ordinal))
                    return value;
            }

            return DefaultProfileName;
        }

        return DefaultProfileName;
    }
}
