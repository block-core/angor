using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Sections.Funds;

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

    public SendFundsModal()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick);
    }

    private ShellViewModel? ShellVm =>
        this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;

    public void OnBackdropCloseRequested() { }

    /// <summary>
    /// Set the source wallet info shown in the "From" box.
    /// Called by FundsView before showing the modal.
    /// </summary>
    public void SetWallet(string name, string type, string balance)
    {
        FromWalletName.Text = name;
        FromWalletType.Text = type;
        FromBalance.Text = balance;
        _walletBalance = balance.Replace(" BTC", "").Trim();
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
                // Simulate sending — show success
                SummaryAmount.Text = string.IsNullOrEmpty(AmountInput.Text)
                    ? "0.00000000 BTC"
                    : $"{AmountInput.Text} BTC";
                ShowStep("success");
                break;

            case "BtnCopyTxid":
                // Stub: copy txid to clipboard
                break;

            case "BtnDone":
                ShellVm?.HideModal();
                break;
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
}
