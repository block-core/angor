using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Angor.Shared.Services;
using App.UI.Shared;
using App.UI.Shared.Helpers;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace App.UI.Sections.Funds;

/// <summary>
/// Send Funds Modal — Vue Funds.vue send flow:
///   Step 1 "form":    From wallet → address → amount (% buttons) → Send
///   Step 2 "success": Green check → summary (amount, fee, txid) → Done
///
/// DataContext = FundsViewModel (set by FundsView when opening).
/// The wallet name/balance are set via SetWallet() before showing.
/// Fee selection is handled via the reusable FeeSelectionPopup
/// when the user clicks Send.
/// </summary>
public partial class SendFundsModal : UserControl, IBackdropCloseable
{
    private string _walletBalance = "0.00000000";
    private string _walletId = "";
    private string _lastTxId = "";

    private ICurrencyService CurrencyService =>
        App.Services.GetRequiredService<ICurrencyService>();

    public SendFundsModal()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick);

        // Clear errors on input (Vue: @input clears errors)
        AddressInput.TextChanged += (_, _) => ClearSendErrors();
        AmountInput.TextChanged += (_, _) => ClearSendErrors();
    }

    private ShellViewModel? GetShellVm()
    {
        var shellView = this.FindAncestorOfType<ShellView>();
        if (shellView?.DataContext is ShellViewModel vm1) return vm1;

        // Fallback to service locator when removed from visual tree
        return App.Services.GetService<ShellViewModel>();
    }

    public void OnBackdropCloseRequested() { }

    /// <summary>
    /// Set the source wallet info shown in the "From" box.
    /// Called by FundsView before showing the modal.
    /// </summary>
    public void SetWallet(string name, string type, string balance, string? walletId = null)
    {
        FromWalletName.Text = name;
        FromWalletType.Text = type;
        FromBalance.Text = balance;
        _walletBalance = balance.Replace($" {CurrencyService.Symbol}", "").Trim();
        _walletId = walletId ?? "";
    }

    /// <summary>
    /// Pre-fill the amount input (used when sending selected UTXOs from WalletDetailModal).
    /// </summary>
    public void PrefillAmount(double amount)
    {
        AmountInput.Text = amount.ToString("F8", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "CloseForm":
            case "BtnCancel":
                GetShellVm()?.HideModal();
                break;

            case "BtnPct25":
                SetPercentage(0.25);
                break;
            case "BtnPct50":
                SetPercentage(0.50);
                break;
            case "BtnPct75":
                SetPercentage(0.75);
                break;
            case "BtnPct100":
                SetPercentage(1.0);
                break;

            case "BtnSend":
                if (!ValidateSendForm()) return;
                _ = SendWithFeePopupAsync();
                break;

            case "BtnCopyTxid":
                ClipboardHelper.CopyToClipboard(this, _lastTxId);
                break;

            case "BtnExploreTxid":
                var networkService = App.Services.GetRequiredService<INetworkService>();
                ExplorerHelper.OpenTransaction(networkService, _lastTxId);
                break;

            case "BtnDone":
                GetShellVm()?.HideModal();
                break;
        }
    }

    /// <summary>
    /// Shows the fee selection popup, then sends the transaction with the selected fee rate.
    /// </summary>
    private async Task SendWithFeePopupAsync()
    {
        var shellVm = GetShellVm();
        if (shellVm == null) return;

        // Show fee popup (replaces send modal as shell modal content)
        var feeRate = await FeeSelectionPopup.ShowAsync(shellVm);

        if (feeRate == null)
        {
            // User cancelled — re-show the send modal
            shellVm.ShowModal(this);
            return;
        }

        // Re-show the send modal for the send operation
        shellVm.ShowModal(this);
        await SendAsync(feeRate.Value);
    }

    private async Task SendAsync(long feeRate)
    {
        if (DataContext is not FundsViewModel fundsVm) return;
        if (string.IsNullOrEmpty(_walletId)) return;

        var address = AddressInput.Text?.Trim() ?? "";
        if (!double.TryParse(AmountInput.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount)) return;

        // Disable send button during operation
        var sendBtn = this.FindControl<Button>("BtnSend");
        if (sendBtn != null) sendBtn.IsEnabled = false;

        var (success, txId) = await fundsVm.SendAsync(_walletId, address, amount, feeRate);

        if (sendBtn != null) sendBtn.IsEnabled = true;

        if (success && txId != null)
        {
            _lastTxId = txId;
            SummaryAmount.Text = CurrencyService.FormatBtc(amount);
            SummaryFee.Text = $"0.00001200 {CurrencyService.Symbol}";
            SummaryTxid.Text = txId;
            ShowStep("success");
        }
        else
        {
            AmountError.Text = "Transaction failed. Please try again.";
            AmountError.IsVisible = true;
        }
    }

    private void SetPercentage(double pct)
    {
        if (double.TryParse(_walletBalance, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var bal))
        {
            AmountInput.Text = (bal * pct).ToString("F8", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void ShowStep(string step)
    {
        FormPanel.IsVisible = step == "form";
        SuccessPanel.IsVisible = step == "success";
    }

    private void ClearSendErrors()
    {
        AddressError.IsVisible = false;
        AmountError.IsVisible = false;
    }

    /// <summary>
    /// Validate address + amount before sending. Returns true if valid.
    /// </summary>
    private bool ValidateSendForm()
    {
        ClearSendErrors();

        if (string.IsNullOrWhiteSpace(AddressInput.Text))
        {
            AddressError.Text = "Address is required";
            AddressError.IsVisible = true;
            return false;
        }

        if (string.IsNullOrWhiteSpace(AmountInput.Text) ||
            !double.TryParse(AmountInput.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount))
        {
            AmountError.Text = "Amount must be greater than 0";
            AmountError.IsVisible = true;
            return false;
        }

        if (amount <= 0)
        {
            AmountError.Text = "Amount must be greater than 0";
            AmountError.IsVisible = true;
            return false;
        }

        if (amount < 0.00001)
        {
            AmountError.Text = $"Minimum 0.00001 {CurrencyService.Symbol}";
            AmountError.IsVisible = true;
            return false;
        }

        if (double.TryParse(_walletBalance, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var bal) && amount > bal)
        {
            AmountError.Text = "Amount exceeds balance";
            AmountError.IsVisible = true;
            return false;
        }

        return true;
    }
}
