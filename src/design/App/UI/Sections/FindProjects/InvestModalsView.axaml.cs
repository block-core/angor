using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using App.UI.Sections.Funds;
using App.UI.Shared;
using App.UI.Shared.Helpers;
using App.UI.Shared.Services;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace App.UI.Sections.FindProjects;

/// <summary>
/// Shell-level modal overlay for the Invest flow.
/// Contains Wallet Selector, Invoice/QR, and Success modals.
/// DataContext = InvestPageViewModel.
/// Implements IBackdropCloseable so the shell can notify on backdrop clicks.
/// </summary>
public partial class InvestModalsView : UserControl, IBackdropCloseable
{
    private Border? _selectedWalletBorder;

    /// <summary>
    /// Callback invoked when the user completes the flow (success → "View My Investments").
    /// The parent (InvestPageView) sets this to navigate back to the project list.
    /// </summary>
    public Action? OnNavigateBackToList { get; set; }

    public InvestModalsView()
    {
        InitializeComponent();

        AddHandler(Button.ClickEvent, OnButtonClick);
        AddHandler(Border.PointerPressedEvent, OnBorderPressed, RoutingStrategies.Bubble);
    }

    private InvestPageViewModel? Vm => DataContext as InvestPageViewModel;

    /// <summary>
    /// Called by the shell when the backdrop is clicked.
    /// Handles cleanup logic (resetting VM state) before the shell closes the modal.
    /// </summary>
    public void OnBackdropCloseRequested()
    {
        if (Vm?.IsSuccess == true)
        {
            OnNavigateBackToList?.Invoke();
        }
        else
        {
            Vm?.CloseModal();
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        // Walk up from e.Source to find the Button — e.Source may be a child (TextBlock, Icon, Panel)
        Button? btn = e.Source as Button;
        if (btn == null)
        {
            var control = e.Source as Avalonia.Controls.Control;
            while (control != null)
            {
                if (control is Button b) { btn = b; break; }
                control = control.Parent as Avalonia.Controls.Control;
            }
        }
        if (btn == null) return;

        switch (btn.Name)
        {
            case "CloseWalletSelector":
                Vm?.CloseModal();
                GetShellVm()?.HideModal();
                break;

            case "PayWithWalletButton":
                _ = PayWithWalletViaFeePopupAsync();
                break;

            case "PayInvoiceInsteadButton":
                Vm?.ShowInvoice();
                break;

            case "CloseInvoice":
                Vm?.CloseModal();
                GetShellVm()?.HideModal();
                break;

            case "CopyInvoiceButton":
                ClipboardHelper.CopyToClipboard(this, Vm?.InvoiceString);
                break;

            case "ViewInvestmentsButton":
                GetShellVm()?.HideModal();
                OnNavigateBackToList?.Invoke();
                break;
        }
    }

    private ShellViewModel? GetShellVm()
    {
        var shellView = this.FindAncestorOfType<ShellView>();
        return shellView?.DataContext as ShellViewModel;
    }

    /// <summary>
    /// Open the CreateWalletModal pre-set to the "import" step (BIP-39 seed entry).
    /// On dismissal (success, cancel, or backdrop) re-shows the invest modal.
    /// If a wallet was imported, refreshes the wallet list so the new wallet appears.
    /// </summary>
    private void OpenImportWalletModal()
    {
        var shellVm = GetShellVm();
        if (shellVm == null) return;

        // Cancel any running invoice monitoring while the import dialog is up.
        Vm?.SelectNetworkTab(NetworkTab.OnChain);

        var fundsVm = global::App.App.Services.GetRequiredService<FundsViewModel>();
        var modal = new CreateWalletModal { DataContext = fundsVm };

        // Skip the choice step — go straight to seed import.
        modal.ShowStep("import");

        modal.OnDismissed = async walletCreated =>
        {
            if (walletCreated)
            {
                // Reload wallets so the new one appears in the wallet selector / invoice flow.
                var walletContext = global::App.App.Services.GetRequiredService<IWalletContext>();
                await walletContext.ReloadAsync();
            }

            // Re-show the invest modal so the user can continue the flow.
            shellVm.ShowModal(this);
        };

        shellVm.ShowModal(modal);
    }

    /// <summary>
    /// Show fee selection popup, then proceed with wallet payment using the selected fee rate.
    /// Follows the same hide-modal/show-popup/re-show-modal pattern used in DeployFlowOverlay and SendFundsModal.
    /// </summary>
    private async Task PayWithWalletViaFeePopupAsync()
    {
        var shellVm = GetShellVm();
        if (shellVm == null || Vm == null) return;

        var feeRate = await FeeSelectionPopup.ShowAsync(shellVm);

        if (feeRate == null)
        {
            // User cancelled — re-show the invest modals
            shellVm.ShowModal(this);
            return;
        }

        Vm.SelectedFeeRate = feeRate.Value;
        shellVm.ShowModal(this);
        Vm.PayWithWallet();
    }

    private void OnBorderPressed(object? sender, PointerPressedEventArgs e)
    {
        var source = e.Source as Control;
        Border? found = null;
        string? foundName = null;

        while (source != null)
        {
            if (source is Border b && !string.IsNullOrEmpty(b.Name))
            {
                var name = b.Name;
                if (name == "WalletBorder"
                    || name == "OnChainTabBorder"
                    || name == "LightningTabBorder"
                    || name == "ImportTabBorder")
                {
                    found = b;
                    foundName = name;
                    break;
                }
            }
            source = source.Parent as Control;
        }

        if (found == null || foundName == null) return;

        switch (foundName)
        {
            case "WalletBorder":
                if (found.DataContext is WalletInfo wallet)
                {
                    Vm?.SelectWallet(wallet);
                    _selectedWalletBorder = WalletSelectionHelper.UpdateWalletSelection(_selectedWalletBorder, found);
                    e.Handled = true;
                }
                break;

            case "OnChainTabBorder":
                Vm?.SelectNetworkTab(NetworkTab.OnChain);
                e.Handled = true;
                break;
            case "LightningTabBorder":
                Vm?.SelectNetworkTab(NetworkTab.Lightning);
                e.Handled = true;
                break;
            case "ImportTabBorder":
                OpenImportWalletModal();
                e.Handled = true;
                break;
        }
    }
}

