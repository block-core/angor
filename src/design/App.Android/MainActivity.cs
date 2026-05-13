using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Views;
using App.UI.Shared.Helpers;
using Avalonia.Android;
using Avalonia.Threading;

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
