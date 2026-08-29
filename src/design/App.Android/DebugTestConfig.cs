#if DEBUG
using System;

namespace App.Android;

/// <summary>
/// Test-only configuration injection for Android UAT runs (Debug builds only).
///
/// Android has no equivalent of launching a process with environment variables, so
/// UAT hosts set persistent system properties via adb instead:
///
///   adb shell setprop debug.angor.test_api 1
///   adb shell setprop debug.angor.network signet
///   adb shell setprop debug.angor.indexer_url http://10.0.2.2:48080
///   adb shell setprop debug.angor.relay_urls ws://10.0.2.2:47777,ws://10.0.2.2:47778
///   adb shell setprop debug.angor.faucet_url http://10.0.2.2:48500
///   adb shell setprop debug.angor.profile Alice
///
/// This class reads them at startup (before the DI container is built) and maps them
/// onto the same ANGOR_* environment variables that CompositionRoot,
/// EnvOverrideNetworkStorage and AutomationServer already consume on desktop.
///
/// Properties persist until reboot; clear with: adb shell setprop debug.angor.&lt;name&gt; ''
/// Only "debug.*" properties are settable from a non-rooted adb shell.
/// </summary>
internal static class DebugTestConfig
{
    private static readonly (string Property, string EnvVar)[] Mappings =
    {
        ("debug.angor.test_api", "ANGOR_TEST_API"),
        ("debug.angor.network", "ANGOR_NETWORK"),
        ("debug.angor.indexer_url", "ANGOR_INDEXER_URL"),
        ("debug.angor.relay_urls", "ANGOR_RELAY_URLS"),
        ("debug.angor.faucet_url", "ANGOR_FAUCET_BASE_URL"),
        ("debug.angor.profile", "ANGOR_PROFILE"),
    };

    /// <summary>
    /// Reads debug.angor.* system properties and sets the corresponding ANGOR_*
    /// environment variables for the current process. Must run before the DI
    /// container is built (i.e. before Avalonia framework initialization completes).
    /// </summary>
    public static void ApplyFromSystemProperties()
    {
        foreach (var (property, envVar) in Mappings)
        {
            var value = GetSystemProperty(property);
            if (!string.IsNullOrWhiteSpace(value))
            {
                Environment.SetEnvironmentVariable(envVar, value);
                global::Android.Util.Log.Info("DebugTestConfig", $"{property} -> {envVar}={value}");
            }
        }
    }

    private static string? GetSystemProperty(string name)
    {
        try
        {
            // Use Android's hidden SystemProperties class via JNI reflection.
            // Process.Start("getprop") doesn't work from managed Android code.
            using var systemProperties = Java.Lang.Class.ForName("android.os.SystemProperties");
            if (systemProperties == null) return null;

            using var getMethod = systemProperties.GetMethod("get",
                Java.Lang.Class.FromType(typeof(Java.Lang.String)),
                Java.Lang.Class.FromType(typeof(Java.Lang.String)));

            if (getMethod == null) return null;

            var result = getMethod.Invoke(null,
                new Java.Lang.String(name),
                new Java.Lang.String(""))?.ToString();

            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("DebugTestConfig", $"GetSystemProperty({name}) failed: {ex.Message}");
            return null;
        }
    }
}
#endif
