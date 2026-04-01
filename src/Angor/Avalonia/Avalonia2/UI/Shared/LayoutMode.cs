using Avalonia;
using ReactiveUI;

namespace Avalonia2.UI.Shared;

/// <summary>
/// Responsive layout mode — matches the Vue prototype's breakpoints.
/// Vue: lg = 1024px (sidebar visible), xl = 1280px (header stats visible).
///
/// Since this is a desktop app (not a browser), we track the window width
/// and derive the same breakpoints the Vue prototype uses with Tailwind CSS.
/// </summary>
public enum LayoutMode
{
    /// <summary>Width &lt; 768px. Single column, bottom tab bar, minimal chrome.</summary>
    Mobile,

    /// <summary>768px &lt;= Width &lt; 1024px. Bottom tab bar still visible, two-column layouts may stack.</summary>
    Tablet,

    /// <summary>Width &gt;= 1024px. Full sidebar, header, multi-column layouts.</summary>
    Desktop,
}

/// <summary>
/// Reactive singleton that tracks the app window width and exposes
/// the current <see cref="LayoutMode"/> plus convenience booleans.
///
/// Sections and controls observe these properties to adapt their layout.
///
/// Usage:
///   - ShellView subscribes to MainWindow.ClientSizeChanged and calls UpdateWidth()
///   - Any view can inject/access LayoutModeService.Instance to observe IsMobile, IsTablet, etc.
///   - XAML bindings use the AdaptiveLayout attached property instead (see below)
///
/// Breakpoints (matching Vue prototype Tailwind defaults):
///   - Mobile:  width &lt; 768
///   - Tablet:  768 &lt;= width &lt; 1024
///   - Desktop: width &gt;= 1024
/// </summary>
public partial class LayoutModeService : ReactiveObject
{
    public static LayoutModeService Instance { get; } = new();

    // Breakpoint thresholds (px) — matches Vue/Tailwind
    public const double MobileBreakpoint = 768;
    public const double TabletBreakpoint = 1024;

    [Reactive] private LayoutMode currentMode = LayoutMode.Desktop;
    [Reactive] private double windowWidth;

    /// <summary>True when the window is narrower than 1024px (mobile or tablet).</summary>
    public bool IsCompact => CurrentMode != LayoutMode.Desktop;

    /// <summary>True when the window is narrower than 768px.</summary>
    public bool IsMobile => CurrentMode == LayoutMode.Mobile;

    /// <summary>True when the window is 768-1023px.</summary>
    public bool IsTablet => CurrentMode == LayoutMode.Tablet;

    /// <summary>True when the window is 1024px or wider.</summary>
    public bool IsDesktop => CurrentMode == LayoutMode.Desktop;

    private LayoutModeService()
    {
        // Raise convenience booleans whenever mode changes
        this.WhenAnyValue(x => x.CurrentMode)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsCompact));
                this.RaisePropertyChanged(nameof(IsMobile));
                this.RaisePropertyChanged(nameof(IsTablet));
                this.RaisePropertyChanged(nameof(IsDesktop));
            });
    }

    /// <summary>
    /// Called by ShellView whenever the window size changes.
    /// Recalculates the layout mode from the new width.
    /// When the mode crosses a breakpoint, the change is deferred to the next
    /// UI frame to avoid mutating Grid definitions while Avalonia's layout
    /// pass is still active (which causes SIGABRT on macOS).
    /// </summary>
    public void UpdateWidth(double width)
    {
        WindowWidth = width;
        var newMode = width switch
        {
            < MobileBreakpoint => LayoutMode.Mobile,
            < TabletBreakpoint => LayoutMode.Tablet,
            _ => LayoutMode.Desktop,
        };

        // Set mode synchronously — the SIGABRT crash is prevented by ShellView/HomeView
        // using in-place width modification on their grids. Inner views that still use
        // Clear()+Add() need the synchronous update to work correctly.
        CurrentMode = newMode;
    }
}
