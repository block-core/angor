namespace App.UI.Shared;

/// <summary>
/// Lifecycle interface for section views hosted in the shell's content area.
/// On mobile, views stay permanently in the logical tree (inside a Panel with
/// IsVisible toggling) to avoid the ~250ms Avalonia detach/reattach cost per
/// tab switch. This interface replaces the OnAttachedToLogicalTree /
/// OnDetachedFromLogicalTree lifecycle that the ContentControl swap pattern
/// relied on.
///
/// Desktop continues to use ContentControl content swap, but the interface is
/// still called — making lifecycle handling uniform across platforms.
/// </summary>
public interface ISectionView
{
    /// <summary>
    /// Called when this section becomes the active (visible) tab.
    /// Use this to refresh data, resume polling, or re-evaluate bindings.
    /// Runs on the UI thread.
    /// </summary>
    void OnBecameActive();

    /// <summary>
    /// Called when this section is navigated away from.
    /// Use this to pause expensive background work. Do NOT dispose
    /// subscriptions — the view stays alive in the tree.
    /// </summary>
    void OnBecameInactive();
}
