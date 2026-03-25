using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using App.UI.Shared.Helpers;
using App.UI.Shell;

namespace App.UI.Sections.Funds;

/// <summary>
/// Send Funds Modal — Vue Funds.vue send flow:
///   Step 1 "form":    From wallet → address → amount (% buttons) → fee selector → Send
///   Step 2 "success": Green check → summary (amount, fee, txid) → Done
///
/// DataContext = FundsViewModel (set by FundsView when opening).
/// The wallet name/balance are set via SetWallet() before showing.
/// </summary>
public partial class SendFundsModal : UserControl, IBackdropCloseable
{
    private string _walletBalance = "0.00000000";
    private string _walletId = "";
    private string _lastTxId = "";

    public SendFundsModal()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick);

        // Clear errors on input (Vue: @input clears errors)
        AddressInput.TextChanged += (_, _) => ClearSendErrors();
        AmountInput.TextChanged += (_, _) => ClearSendErrors();
    }

    private ShellViewModel? ShellVm =>
        this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;

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
        _walletBalance = balance.Replace(" BTC", "").Trim();
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
                ShellVm?.HideModal();
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

            case "BtnFeeLow":
            case "BtnFeeMedium":
            case "BtnFeeHigh":
                SelectFee(btn.Name);
                break;

            case "BtnSend":
                if (!ValidateSendForm()) return;
                _ = SendAsync();
                break;

            case "BtnCopyTxid":
                ClipboardHelper.CopyToClipboard(this, _lastTxId);
                break;

            case "BtnDone":
                ShellVm?.HideModal();
                break;
        }
    }

    private long GetSelectedFeeRate()
    {
        if (BtnFeeHigh.Classes.Contains("FeeSelected")) return 50;
        if (BtnFeeLow.Classes.Contains("FeeSelected")) return 5;
        return 20; // medium/default
    }

    private async Task SendAsync()
    {
        if (DataContext is not FundsViewModel fundsVm) return;
        if (string.IsNullOrEmpty(_walletId)) return;

        var address = AddressInput.Text?.Trim() ?? "";
        if (!double.TryParse(AmountInput.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount)) return;

        var feeRate = GetSelectedFeeRate();

        // Disable send button during operation
        var sendBtn = this.FindControl<Button>("BtnSend");
        if (sendBtn != null) sendBtn.IsEnabled = false;

        var (success, txId) = await fundsVm.SendAsync(_walletId, address, amount, feeRate);

        if (sendBtn != null) sendBtn.IsEnabled = true;

        if (success && txId != null)
        {
            _lastTxId = txId;
            SummaryAmount.Text = $"{amount:F8} BTC";
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

    private void SelectFee(string selectedName)
    {
        foreach (var btn in new[] { BtnFeeLow, BtnFeeMedium, BtnFeeHigh })
            btn.Classes.Set("FeeSelected", btn.Name == selectedName);
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
            AmountError.Text = "Minimum 0.00001 BTC";
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
