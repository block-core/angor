using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace App.Test.Uat.Helpers;

/// <summary>
/// Manages a child App.Desktop process for a specific user profile.
/// The child process runs with ANGOR_TEST_API=1, exposing an HTTP automation endpoint.
/// Dispose kills the process.
/// </summary>
public sealed class TestProcessHost : IAsyncDisposable
{
    private readonly Process process;

    public TestAutomationClient Client { get; }
    public string ProfileName { get; }
    public int Port { get; }

    private TestProcessHost(Process process, TestAutomationClient client, string profileName, int port)
    {
        this.process = process;
        Client = client;
        ProfileName = profileName;
        Port = port;
    }

    /// <summary>
    /// Launches an App.Desktop process for the given profile with the test automation server enabled.
    /// Waits for the /health endpoint to respond before returning.
    /// </summary>
    /// <param name="profileName">The --profile value for data isolation.</param>
    /// <param name="extraEnvVars">Additional environment variables to set (e.g., ANGOR_INDEXER_URL).</param>
    /// <param name="healthTimeout">How long to wait for the app to become ready.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<TestProcessHost> LaunchAsync(
        string profileName,
        Dictionary<string, string>? extraEnvVars = null,
        TimeSpan? healthTimeout = null,
        CancellationToken ct = default)
    {
        var port = GetFreePort();
        var exePath = FindDesktopExe();

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"--profile=\"{profileName}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // Core automation env vars
        psi.Environment["ANGOR_TEST_API"] = "1";
        psi.Environment["ANGOR_TEST_API_PORT"] = port.ToString();

        // Inherit test infrastructure env vars
        foreach (var key in new[] { "ANGOR_INDEXER_URL", "ANGOR_RELAY_URLS", "ANGOR_FAUCET_BASE_URL", "ANGOR_NETWORK" })
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(value))
            {
                psi.Environment[key] = value;
            }
        }

        // Linux/Docker: set DISPLAY for Xvfb
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi.Environment["DISPLAY"] = Environment.GetEnvironmentVariable("DISPLAY") ?? ":99";
        }

        // Extra env vars from caller
        if (extraEnvVars != null)
        {
            foreach (var (key, value) in extraEnvVars)
            {
                psi.Environment[key] = value;
            }
        }

        var process = new Process { StartInfo = psi };
        process.Start();

        // Capture stdout/stderr to temp log files
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp", "opencode");
        Directory.CreateDirectory(logDir);

        var stdoutLog = Path.Combine(logDir, $"test-{profileName}-stdout.log");
        var stderrLog = Path.Combine(logDir, $"test-{profileName}-stderr.log");

        _ = Task.Run(() => PipeToFile(process.StandardOutput, stdoutLog), ct);
        _ = Task.Run(() => PipeToFile(process.StandardError, stderrLog), ct);

        Console.WriteLine($"[TestProcessHost] Launched PID={process.Id} for profile '{profileName}' on port {port}");
        Console.WriteLine($"[TestProcessHost] Stdout log: {stdoutLog}");
        Console.WriteLine($"[TestProcessHost] Stderr log: {stderrLog}");

        var client = new TestAutomationClient($"http://127.0.0.1:{port}");

        // Wait for /health
        var deadline = DateTime.UtcNow + (healthTimeout ?? TimeSpan.FromSeconds(60));
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"App.Desktop process for profile '{profileName}' exited with code {process.ExitCode} before becoming ready. " +
                    $"Check logs at {stderrLog}");
            }

            try
            {
                var ready = await client.HealthCheckAsync(ct);
                if (ready)
                {
                    Console.WriteLine($"[TestProcessHost] Profile '{profileName}' ready on port {port}");
                    return new TestProcessHost(process, client, profileName, port);
                }
            }
            catch
            {
                // Not ready yet
            }

            await Task.Delay(500, ct);
        }

        // Timed out — kill process
        try { process.Kill(entireProcessTree: true); } catch { }
        throw new TimeoutException(
            $"App.Desktop process for profile '{profileName}' did not become ready within timeout. " +
            $"Check logs at {stderrLog}");
    }

    public async ValueTask DisposeAsync()
    {
        int pid = -1;
        try { pid = process.Id; } catch { }

        if (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch
            {
                // Best effort
            }
        }

        Client.Dispose();
        process.Dispose();
        Console.WriteLine($"[TestProcessHost] Disposed profile '{ProfileName}' (PID was {pid})");
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindDesktopExe()
    {
        // Find the App.Desktop executable relative to the test assembly
        // The test project references App which references nothing about Desktop,
        // so we need to locate it by convention.

        // Strategy 1: ANGOR_DESKTOP_EXE env var (explicit override)
        var envExe = Environment.GetEnvironmentVariable("ANGOR_DESKTOP_EXE");
        if (!string.IsNullOrEmpty(envExe) && File.Exists(envExe))
        {
            return envExe;
        }

        // Strategy 2: Navigate from the test assembly location to the Desktop build output
        var testDir = Path.GetDirectoryName(typeof(TestProcessHost).Assembly.Location)!;
        // testDir is typically: src/design/App.Test.Uat/bin/Debug/net10.0
        // Desktop is at:        src/design/App.Desktop/bin/Debug/net10.0/Angor2.exe (or Angor2 on Linux)
        var designRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Angor2.exe" : "Angor2";
        var configuration = testDir.Contains("Release", StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
        var candidate = Path.Combine(designRoot, "App.Desktop", "bin", configuration, "net10.0", exeName);

        if (File.Exists(candidate))
        {
            return candidate;
        }

        // Strategy 3: search upward for the sln root
        var current = testDir;
        while (current != null)
        {
            var desktopDir = Path.Combine(current, "App.Desktop", "bin");
            if (Directory.Exists(desktopDir))
            {
                var found = Directory.GetFiles(desktopDir, exeName, SearchOption.AllDirectories).FirstOrDefault();
                if (found != null)
                {
                    return found;
                }
            }

            current = Path.GetDirectoryName(current);
        }

        throw new FileNotFoundException(
            $"Could not find App.Desktop executable '{exeName}'. " +
            "Build App.Desktop first, or set ANGOR_DESKTOP_EXE environment variable.");
    }

    private static async Task PipeToFile(StreamReader reader, string path)
    {
        try
        {
            using var writer = new StreamWriter(path, append: false);
            while (await reader.ReadLineAsync() is { } line)
            {
                await writer.WriteLineAsync(line);
                await writer.FlushAsync();
            }
        }
        catch
        {
            // Process exited
        }
    }
}
