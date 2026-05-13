using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Views;
using Angor.Data.Documents.Interfaces;
using App.UI.Shared.Helpers;
using App.UI.Shell;
using Avalonia.Android;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.Android;

[Activity(
    Label = "Angor",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.Portrait,
    WindowSoftInputMode = SoftInput.AdjustNothing,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    private PerfTabReceiver? _perfReceiver;
    private bool _handlingPlatformBack;

    protected override void OnResume()
    {
        base.OnResume();
        _perfReceiver = new PerfTabReceiver();
        var filter = new IntentFilter("io.angor.app.PERF_TAB");
        RegisterReceiver(_perfReceiver, filter, ReceiverFlags.Exported);
    }

    protected override void OnPause()
    {
        base.OnPause();
        if (_perfReceiver != null)
        {
            UnregisterReceiver(_perfReceiver);
            _perfReceiver = null;
        }
    }

    protected override void OnStop()
    {
        base.OnStop();

        // Flush all pending state to disk before the process may be killed.
        // Android can terminate the process at any point after OnStop returns.
        try
        {
            var services = global::App.App.Services;
            if (services == null) return;

            // Checkpoint LiteDB to flush the WAL into the main data file
            var db = services.GetService<IAngorDocumentDatabase>();
            if (db != null)
            {
                db.CheckpointAsync().GetAwaiter().GetResult();
            }

            // Flush prototype settings (selected wallet ID, theme, etc.)
            var settings = services.GetService<PrototypeSettings>();
            settings?.FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            try
            {
                var logger = global::App.App.Services?
                    .GetService<ILoggerFactory>()?
                    .CreateLogger<MainActivity>();
                logger?.LogError(ex, "Failed to flush data in OnStop");
            }
            catch
            {
                // Swallow — logging infrastructure may already be disposed
            }
        }
    }

    public override void OnBackPressed()
    {
        if (_handlingPlatformBack)
        {
            base.OnBackPressed();
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (ShellService.TryHandlePlatformBack()) return;

            _handlingPlatformBack = true;
            OnBackPressed();
            _handlingPlatformBack = false;
        });
    }
}

/// <summary>
/// Receives adb broadcast intents to drive tab switches for automated perf testing.
/// Usage: adb shell am broadcast -a io.angor.app.PERF_TAB --es tab "Investor"
/// Valid tabs: Home, Investor, Founder, Funds, Settings
/// </summary>
[BroadcastReceiver(Exported = true)]
public class PerfTabReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var tab = intent?.GetStringExtra("tab");
        global::Android.Util.Log.Info("PerfTab", $"Received broadcast tab={tab}");
        if (string.IsNullOrEmpty(tab)) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            global::Android.Util.Log.Info("PerfTab", $"Dispatching SwitchTabForPerf tab={tab}");
            global::App.UI.Shell.ShellViewModel.SwitchTabForPerf(tab);
        });
    }
}
