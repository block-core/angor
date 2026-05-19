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
    private Button? _selectedWalletButton;

    public PaymentFlowView()
    {
        InitializeComponent();

        AddHandler(Button.ClickEvent, OnButtonClick);
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

            case "WalletButton":
                if (btn.CommandParameter is WalletInfo wallet)
                {
                    Vm?.SelectWallet(wallet);
                    _selectedWalletButton = WalletSelectionHelper.UpdateWalletSelection(_selectedWalletButton, btn);
                }
                break;

            case "OnChainTabButton":
                Vm?.SelectNetworkTab(NetworkTab.OnChain);
                break;
            case "LightningTabButton":
                Vm?.SelectNetworkTab(NetworkTab.Lightning);
                break;
            case "ImportTabButton":
                OpenImportWalletModal();
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
}
