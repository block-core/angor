using Microsoft.Extensions.Configuration;

namespace App.Test.Integration.Helpers;

/// <summary>
/// Reads test secrets from environment variables (CI) or .NET user secrets (local dev).
/// Environment variables take precedence.
///
/// Local setup: dotnet user-secrets set KEY VALUE
/// CI setup: GitHub Actions secret → env var
/// </summary>
public static class TestSecrets
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddUserSecrets(typeof(TestSecrets).Assembly, optional: true)
        .AddEnvironmentVariables()
        .Build();

    public static string? Get(string key) => Configuration[key];
}
