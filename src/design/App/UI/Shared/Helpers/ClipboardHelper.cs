using Avalonia.Controls;
using Avalonia.VisualTree;
using App.UI.Shell;

namespace App.UI.Shared.Helpers;

/// <summary>
/// Centralized clipboard helper extracted from 4 duplicated CopyToClipboard methods
/// in InvestModalsView, InvestPageView, DeployFlowOverlay, and FundersView.
/// Now also triggers a shell-level toast notification after successful copy.
/// </summary>
public static class ClipboardHelper
{
    /// <summary>
    /// Copy text to the system clipboard via the TopLevel clipboard API,
    /// then show a "Copied to clipboard" toast via the shell.
    /// </summary>
    /// <param name="control">Any attached control (used to resolve TopLevel and ShellView).</param>
    /// <param name="text">The text to copy. No-ops if null or empty.</param>
    public static async void CopyToClipboard(Control control, string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var clipboard = TopLevel.GetTopLevel(control)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(text);
        }

        // Show toast — walk visual tree to find ShellViewModel
        // Vue: showCopyToast = true, copyToastMessage = 'Copied to clipboard', timeout 2000ms
        var shellVm = control.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
        shellVm?.ShowToast("Copied to clipboard");
    }
}
