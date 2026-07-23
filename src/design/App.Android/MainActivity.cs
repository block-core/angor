using System;
using System.Diagnostics;
using System.Threading.Tasks;
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
    Icon = "@mipmap/icon",
    RoundIcon = "@mipmap/icon_round",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.Portrait,
    WindowSoftInputMode = SoftInput.AdjustNothing,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    private PerfTabReceiver? _perfReceiver;
    private bool _handlingPlatformBack;

    private ILogger? GetLogger()
    {
        try
        {
            return global::App.App.Services?
                .GetService<ILoggerFactory>()?
                .CreateLogger<MainActivity>();
        }
        catch
        {
            return null;
        }
    }

    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        GetLogger()?.LogInformation("Lifecycle: OnCreate taskId={TaskId} intent={Intent}", TaskId, Intent?.Action);
    }

    protected override void OnResume()
    {
        base.OnResume();
        GetLogger()?.LogInformation("Lifecycle: OnResume taskId={TaskId}", TaskId);
        _perfReceiver = new PerfTabReceiver();
        var filter = new IntentFilter("io.angor.app.PERF_TAB");
        RegisterReceiver(_perfReceiver, filter, ReceiverFlags.Exported);
    }

    protected override void OnPause()
    {
        GetLogger()?.LogInformation("Lifecycle: OnPause taskId={TaskId}", TaskId);
        base.OnPause();
        if (_perfReceiver != null)
        {
            UnregisterReceiver(_perfReceiver);
            _perfReceiver = null;
        }
    }

    protected override void OnStop()
    {
        var logger = GetLogger();
        var sw = Stopwatch.StartNew();
        logger?.LogInformation("Lifecycle: OnStop BEGIN taskId={TaskId}", TaskId);

        base.OnStop();

        // Flush all pending state to disk before the process may be killed.
        // Android can terminate the process at any point after OnStop returns.
        try
        {
            var services = global::App.App.Services;
            if (services == null)
            {
                logger?.LogWarning("Lifecycle: OnStop — services is null, skipping flush");
                return;
            }

            // Run flush on a thread-pool thread so the main thread is not blocked.
            // LiteDB's Checkpoint() is synchronous internally and can take hundreds
            // of milliseconds — blocking the main thread triggers Android's ANR dialog.
            bool completed = Task.Run(() =>
            {
                var flushSw = Stopwatch.StartNew();

                var db = services.GetService<IAngorDocumentDatabase>();
                if (db != null)
                {
                    db.CheckpointAsync().GetAwaiter().GetResult();
                    logger?.LogInformation("Lifecycle: OnStop checkpoint done in {Ms}ms", flushSw.ElapsedMilliseconds);
                }

                var settings = services.GetService<PrototypeSettings>();
                if (settings != null)
                {
                    settings.FlushAsync().GetAwaiter().GetResult();
                    logger?.LogInformation("Lifecycle: OnStop settings flush done in {Ms}ms", flushSw.ElapsedMilliseconds);
                }
            }).Wait(TimeSpan.FromSeconds(3));

            if (!completed)
            {
                logger?.LogWarning("Lifecycle: OnStop flush TIMED OUT after 3s");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Lifecycle: OnStop flush FAILED after {Ms}ms", sw.ElapsedMilliseconds);
        }

        logger?.LogInformation("Lifecycle: OnStop END totalMs={Ms}", sw.ElapsedMilliseconds);
    }

    protected override void OnDestroy()
    {
        GetLogger()?.LogInformation("Lifecycle: OnDestroy taskId={TaskId} isFinishing={IsFinishing}", TaskId, IsFinishing);
        base.OnDestroy();
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        GetLogger()?.LogInformation("Lifecycle: OnNewIntent action={Action} flags={Flags}", intent?.Action, intent?.Flags);
    }

    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e?.KeyCode == Keycode.Back)
        {
            if (e.Action == KeyEventActions.Down && CanHandleShellBack())
                return true;

            if (e.Action == KeyEventActions.Up && TryHandleShellBack())
                return true;
        }

        return base.DispatchKeyEvent(e);
    }

    public override void OnBackPressed()
    {
        if (_handlingPlatformBack)
        {
            base.OnBackPressed();
            return;
        }

        if (TryHandleShellBack())
            return;

        _handlingPlatformBack = true;
        base.OnBackPressed();
        _handlingPlatformBack = false;
    }

    private static bool CanHandleShellBack()
    {
        if (Dispatcher.UIThread.CheckAccess())
            return ShellService.CanHandlePlatformBack();

        return Dispatcher.UIThread.InvokeAsync(ShellService.CanHandlePlatformBack).GetAwaiter().GetResult();
    }

    private static bool TryHandleShellBack()
    {
        if (Dispatcher.UIThread.CheckAccess())
            return ShellService.TryHandlePlatformBack();

        return Dispatcher.UIThread.InvokeAsync(ShellService.TryHandlePlatformBack).GetAwaiter().GetResult();
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
