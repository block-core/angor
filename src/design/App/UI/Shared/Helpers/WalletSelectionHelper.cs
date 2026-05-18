using Avalonia;

namespace App.UI.Shared.Helpers;

/// <summary>
/// Centralized wallet selection visual update helper.
/// Extracted from duplicated UpdateWalletSelection methods in InvestModalsView and DeployFlowOverlay.
/// Both views use identical CSS class toggling: "WalletSelected" on wallet card elements.
/// </summary>
public static class WalletSelectionHelper
{
    /// <summary>
    /// Update wallet card visual states via CSS class toggling.
    /// Deselects the previous element and selects the new one — no tree walk.
    /// </summary>
    /// <param name="previousSelected">Previously selected element (may be null on first selection).</param>
    /// <param name="newSelected">Newly selected wallet element.</param>
    /// <returns>The newly selected element (caller should store this as their _selectedWallet).</returns>
    public static T UpdateWalletSelection<T>(T? previousSelected, T newSelected) where T : StyledElement
    {
        previousSelected?.Classes.Set("WalletSelected", false);
        newSelected.Classes.Set("WalletSelected", true);
        return newSelected;
    }
}
