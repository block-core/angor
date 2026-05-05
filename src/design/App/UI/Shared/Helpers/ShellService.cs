using App.UI.Shell;

namespace App.UI.Shared.Helpers;

/// <summary>
/// Static service providing decoupled access to shell-level operations (toast, modal, navigation).
/// Eliminates the need for views to walk the visual tree via FindAncestorOfType&lt;ShellView&gt;().
/// Registered once from ShellView constructor; safe to call before registration (no-ops).
/// </summary>
public static class ShellService
{
    private static ShellViewModel? _vm;
    private static Action<bool>? _prepareForThemeChange;

    /// <summary>
    /// Register the shell ViewModel instance. Called once from ShellView constructor.
    /// </summary>
    public static void Register(ShellViewModel vm) => _vm = vm;

    /// <summary>
    /// Register a view-level hook used by the shell to reduce live mobile UI
    /// before and after global theme resource invalidation.
    /// </summary>
    public static void RegisterThemeChangePreparation(Action<bool> prepareForThemeChange)
        => _prepareForThemeChange = prepareForThemeChange;

    public static void PrepareForThemeChange(bool isChanging)
        => _prepareForThemeChange?.Invoke(isChanging);

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
