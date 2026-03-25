using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Shared.Helpers;

/// <summary>
/// Static service providing decoupled access to shell-level operations (toast, modal, navigation).
/// Eliminates the need for views to walk the visual tree via FindAncestorOfType&lt;ShellView&gt;().
/// Registered once from ShellView constructor; safe to call before registration (no-ops).
/// </summary>
public static class ShellService
{
    private static ShellViewModel? _vm;

    /// <summary>
    /// Register the shell ViewModel instance. Called once from ShellView constructor.
    /// </summary>
    public static void Register(ShellViewModel vm) => _vm = vm;

    /// <summary>
    /// Show a toast notification with auto-dismiss.
    /// </summary>
    public static void ShowToast(string message, int durationMs = 2000)
        => _vm?.ShowToast(message, durationMs);

    /// <summary>
    /// Show a modal overlay above the entire app.
    /// </summary>
    public static void ShowModal(object content)
        => _vm?.ShowModal(content);

    /// <summary>
    /// Close the current shell-level modal overlay.
    /// </summary>
    public static void HideModal()
        => _vm?.HideModal();
}
