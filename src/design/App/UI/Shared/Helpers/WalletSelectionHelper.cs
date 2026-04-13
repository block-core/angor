using Avalonia.Controls;

namespace App.UI.Shared.Helpers;

/// <summary>
/// Centralized wallet selection visual update helper.
/// Extracted from duplicated UpdateWalletSelection methods in InvestModalsView and DeployFlowOverlay.
/// Both views use identical CSS class toggling: "WalletSelected" on Border elements named "WalletBorder".
/// </summary>
public static class WalletSelectionHelper
{
    /// <summary>
    /// Update wallet card visual states via CSS class toggling.
    /// Deselects the previous border and selects the new one — no tree walk.
    /// </summary>
    /// <param name="previousSelected">Previously selected border (may be null on first selection).</param>
    /// <param name="newSelected">Newly selected wallet border.</param>
    /// <returns>The newly selected border (caller should store this as their _selectedWalletBorder).</returns>
    public static Border UpdateWalletSelection(Border? previousSelected, Border newSelected)
    {
        previousSelected?.Classes.Set("WalletSelected", false);
        newSelected.Classes.Set("WalletSelected", true);
        return newSelected;
    }
}
