using Microsoft.Extensions.Configuration;

namespace App.Test.Uat.Helpers;

/// <summary>
/// Reads test secrets from environment variables (CI) or .NET user secrets (local dev).
/// Environment variables take precedence.
///
/// Local setup: dotnet user-secrets set KEY VALUE --project src/design/App.Test.Uat
/// CI setup: GitHub Actions secret -> env var
///
/// Required secrets for Lightning tests:
///   THUNDERHUB_URL       - ThunderHub base URL (e.g. https://test.thub1.angor.io)
///   THUNDERHUB_ACCOUNT   - ThunderHub account name (e.g. "lnd-1")
///   THUNDERHUB_PASSWORD  - ThunderHub account password
/// </summary>
public static class TestSecrets
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddUserSecrets(typeof(TestSecrets).Assembly, optional: true)
        .AddEnvironmentVariables()
        .Build();

    public static string? Get(string key) => Configuration[key];
}
