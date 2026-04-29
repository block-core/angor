using Android.App;
using Android.Content;
using Android.Content.PM;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;

namespace App.Android;

[Activity(
    Label = "Angor",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<global::App.App>
{
    private PerfTabReceiver? _perfReceiver;

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        global::App.App.PlatformServices = services =>
        {
            services.AddSingleton<ISecureKeyProvider, AndroidKeyStoreSecureKeyProvider>();
        };

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

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
