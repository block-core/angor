using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using App.UI.Sections.Funds;
using App.UI.Shared.Helpers;
using App.UI.Shared.Services;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace App.UI.Shared.PaymentFlow;

/// <summary>
/// Reusable payment flow modal overlay: Wallet Selector → Invoice/QR → Success.
/// DataContext = PaymentFlowViewModel.
/// Implements IBackdropCloseable so the shell can notify on backdrop clicks.
/// </summary>
public partial class PaymentFlowView : UserControl, IBackdropCloseable
{
    private Border? _selectedWalletBorder;

    public PaymentFlowView()
    {
        InitializeComponent();

        AddHandler(Button.ClickEvent, OnButtonClick);
        AddHandler(Border.PointerPressedEvent, OnBorderPressed, RoutingStrategies.Bubble);
    }

    private PaymentFlowViewModel? Vm => DataContext as PaymentFlowViewModel;

    public void OnBackdropCloseRequested()
    {
        if (Vm?.IsSuccess == true)
        {
            GetShellVm()?.HideModal();
            Vm.OnSuccessButtonClicked();
        }
        else
        {
            Vm?.Reset();
            GetShellVm()?.HideModal();
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
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
                Vm?.Reset();
                GetShellVm()?.HideModal();
                break;

            case "PayWithWalletButton":
                _ = PayWithWalletViaFeePopupAsync();
                break;

            case "PayInvoiceInsteadButton":
                Vm?.ShowInvoice();
                break;

            case "CloseInvoice":
                Vm?.Reset();
                GetShellVm()?.HideModal();
                break;

            case "CopyInvoiceButton":
                ClipboardHelper.CopyToClipboard(this, Vm?.InvoiceString);
                break;

            case "SuccessActionButton":
                GetShellVm()?.HideModal();
                Vm?.OnSuccessButtonClicked();
                break;
        }
    }

    private ShellViewModel? GetShellVm()
    {
        var shellView = this.FindAncestorOfType<ShellView>();
        return shellView?.DataContext as ShellViewModel;
    }

    private void OpenImportWalletModal()
    {
        var shellVm = GetShellVm();
        if (shellVm == null) return;

        Vm?.SelectNetworkTab(NetworkTab.OnChain);

        var fundsVm = global::App.App.Services.GetRequiredService<FundsViewModel>();
        var modal = new CreateWalletModal { DataContext = fundsVm };
        modal.ShowStep("import");

        modal.OnDismissed = async walletCreated =>
        {
            if (walletCreated)
            {
                var walletContext = global::App.App.Services.GetRequiredService<IWalletContext>();
                await walletContext.ReloadAsync();
            }
            shellVm.ShowModal(this);
        };

        shellVm.ShowModal(modal);
    }

    private async Task PayWithWalletViaFeePopupAsync()
    {
        var shellVm = GetShellVm();
        if (shellVm == null || Vm == null) return;

        var feeRate = await FeeSelectionPopup.ShowAsync(shellVm);

        if (feeRate == null)
        {
            shellVm.ShowModal(this);
            return;
        }

        Vm.SelectedFeeRate = feeRate.Value;
        shellVm.ShowModal(this);
        Vm.PayWithWalletCommand.Execute().Subscribe();
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
                if (name is "WalletBorder" or "OnChainTabBorder" or "LightningTabBorder"
                    or "LiquidTabBorder" or "ImportTabBorder")
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
