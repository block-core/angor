using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace App.Automation;

/// <summary>
/// Resolves the visual root for test automation in a lifetime-agnostic way.
/// Desktop uses IClassicDesktopStyleApplicationLifetime.MainWindow; mobile
/// (IActivityApplicationLifetime / ISingleViewApplicationLifetime) resolves the
/// TopLevel that hosts the single root view.
/// </summary>
internal static class AutomationRoot
{
    /// <summary>Returns the current visual root, or null if not yet available.</summary>
    public static TopLevel? Resolve()
    {
        var lifetime = Application.Current?.ApplicationLifetime;

        if (lifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        // Android (IActivityApplicationLifetime) — root view captured by App at startup.
        if (App.MainView is { } mainView && TopLevel.GetTopLevel(mainView) is { } topLevel)
        {
            return topLevel;
        }

        // iOS / WASM fallback
        if (lifetime is ISingleViewApplicationLifetime singleView && singleView.MainView is { } view)
        {
            return TopLevel.GetTopLevel(view);
        }

        return null;
    }
}
