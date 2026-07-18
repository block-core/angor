using System.Net;
using System.Net.Sockets;

namespace App.Test.Uat.Helpers;

/// <summary>
/// Connects to a running Angor Android app's automation server.
///
/// Unlike <see cref="TestProcessHost"/> which manages a desktop process lifecycle,
/// this host assumes the Android app is already running with the automation server
/// active. The caller is responsible for device setup:
///
///   adb shell setprop debug.angor.test_api 1
///   adb shell am force-stop io.angor.app
///   adb shell monkey -p io.angor.app 1
///   adb forward tcp:18721 tcp:18721
///
/// Or use the helper script: src/design/App.Android/scripts/start-uat.ps1
///
/// Set ANGOR_UAT_HOST=android to make TestHostFactory use this host.
/// Optionally set ANGOR_ANDROID_PORT to override the default port (18721).
/// </summary>
public sealed class AndroidTestHost : ITestHost
{
    private const int DefaultPort = 18721;

    public TestAutomationClient Client { get; }
    public string ProfileName { get; }
    public int Port { get; }

    private AndroidTestHost(TestAutomationClient client, string profileName, int port)
    {
        Client = client;
        ProfileName = profileName;
        Port = port;
    }

    /// <summary>
    /// Connects to the Android automation server and waits for /health.
    /// The app must already be running with debug.angor.test_api=1 and
    /// adb forward set up.
    /// </summary>
    public static async Task<AndroidTestHost> ConnectAsync(
        string profileName,
        TimeSpan? healthTimeout = null,
        CancellationToken ct = default)
    {
        var portStr = Environment.GetEnvironmentVariable("ANGOR_ANDROID_PORT");
        var port = int.TryParse(portStr, out var p) ? p : DefaultPort;

        var client = new TestAutomationClient($"http://127.0.0.1:{port}");
        var host = new AndroidTestHost(client, profileName, port);

        Console.WriteLine($"[AndroidTestHost] Connecting to Android automation server on port {port}...");

        var deadline = DateTime.UtcNow + (healthTimeout ?? TimeSpan.FromSeconds(60));
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var ready = await client.HealthCheckAsync(ct);
                if (ready)
                {
                    Console.WriteLine($"[AndroidTestHost] Connected (profile '{profileName}', port {port})");
                    return host;
                }
            }
            catch
            {
                // Not ready yet
            }
            await Task.Delay(500, ct);
        }

        client.Dispose();
        throw new TimeoutException(
            $"Android automation server not reachable on port {port}. " +
            "Make sure the app is running with debug.angor.test_api=1 and adb forward is set up. " +
            "See src/design/App.Android/scripts/start-uat.ps1");
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Console.WriteLine($"[AndroidTestHost] Disconnected (profile '{ProfileName}')");
        return ValueTask.CompletedTask;
    }
}
