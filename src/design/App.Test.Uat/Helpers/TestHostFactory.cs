namespace App.Test.Uat.Helpers;

/// <summary>
/// Creates the appropriate test host based on the ANGOR_UAT_HOST environment variable.
///
///   ANGOR_UAT_HOST=android  → AndroidTestHost (connects to already-running device)
///   (unset or "desktop")    → TestProcessHost (App.Desktop child process)
///
/// For Android, the app must already be running with the automation server active.
/// See src/design/App.Test.Uat/MOBILE-UAT-PLAN.md for setup instructions.
///
/// For multi-profile tests: Android only supports one app instance, so the first
/// host connects to Android and subsequent ones use desktop processes.
/// </summary>
public static class TestHostFactory
{
    private static int androidInstanceCount;

    public static bool IsAndroid =>
        string.Equals(
            Environment.GetEnvironmentVariable("ANGOR_UAT_HOST"),
            "android",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Launches or connects to a test host for the given profile.
    /// </summary>
    public static async Task<ITestHost> LaunchAsync(
        string profileName,
        Dictionary<string, string>? extraEnvVars = null,
        TimeSpan? healthTimeout = null,
        CancellationToken ct = default)
    {
        if (IsAndroid && Interlocked.Increment(ref androidInstanceCount) == 1)
        {
            return await AndroidTestHost.ConnectAsync(
                profileName,
                healthTimeout: healthTimeout ?? TimeSpan.FromSeconds(60),
                ct: ct);
        }

        return await TestProcessHost.LaunchAsync(
            profileName,
            extraEnvVars: extraEnvVars,
            healthTimeout: healthTimeout,
            ct: ct);
    }

    /// <summary>
    /// Resets the Android instance counter between tests.
    /// </summary>
    public static void Reset() => Interlocked.Exchange(ref androidInstanceCount, 0);
}
