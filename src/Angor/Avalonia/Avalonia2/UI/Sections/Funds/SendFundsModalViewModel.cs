using System.Globalization;
using Avalonia2.UI.Shared.Helpers;

namespace Avalonia2.UI.Sections.Funds;

/// <summary>
/// ViewModel for the Send Funds modal — owns all form state, validation, and step transitions.
/// Follows the [Reactive] validation pattern established by CreateProjectViewModel.
/// Vue ref: Funds.vue send flow (form → success).
/// </summary>
public partial class SendFundsModalViewModel : ReactiveObject
{
    /// <summary>Stub txid for the success view — matches the truncated XAML text.</summary>
    private const string StubTxid = "a1b2c3d4e5f67890abcdef1234567890abcdef1234567890abcdef7890abcd";

    // ── Form inputs (two-way bound) ──
    [Reactive] private string addressText = "";
    [Reactive] private string amountText = "";

    // ── Validation errors ──
    [Reactive] private string addressError = "";
    [Reactive] private string amountError = "";

    // ── Wallet info (set by caller before showing modal) ──
    [Reactive] private string walletName = "Wallet";
    [Reactive] private string walletType = "On-Chain";
    [Reactive] private string walletBalanceDisplay = "0.00000000 BTC";

    // ── Step visibility ──
    [Reactive] private bool isFormStep = true;
    [Reactive] private bool isSuccessStep;

    // ── Success view ──
    [Reactive] private string summaryAmountText = "0.00000000 BTC";

    /// <summary>Raw balance string for validation math (no " BTC" suffix).</summary>
    private string _rawBalance = "0.00000000";

    // ── Computed error visibility ──
    public bool HasAddressError => !string.IsNullOrEmpty(AddressError);
    public bool HasAmountError => !string.IsNullOrEmpty(AmountError);

    public SendFundsModalViewModel()
    {
        // Clear errors on input (Vue: @input clears errors)
        this.WhenAnyValue(x => x.AddressText)
            .Subscribe(_ =>
            {
                AddressError = "";
                this.RaisePropertyChanged(nameof(HasAddressError));
            });

        this.WhenAnyValue(x => x.AmountText)
            .Subscribe(_ =>
            {
                AmountError = "";
                this.RaisePropertyChanged(nameof(HasAmountError));
            });
    }

    /// <summary>
    /// Set the source wallet info shown in the "From" box.
    /// Called by the view that opens this modal.
    /// </summary>
    public void SetWallet(string name, string type, string balance)
    {
        WalletName = name;
        WalletType = type;
        WalletBalanceDisplay = balance;
        _rawBalance = balance.Replace(" BTC", "").Trim();
    }

    /// <summary>
    /// Pre-fill the amount input (used when sending selected UTXOs from WalletDetailModal).
    /// </summary>
    public void PrefillAmount(double amount)
    {
        AmountText = amount.ToString("F8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Set amount to a percentage of the wallet balance.
    /// </summary>
    public void SetPercentage(double pct)
    {
        if (double.TryParse(_rawBalance, NumberStyles.Any, CultureInfo.InvariantCulture, out var bal))
        {
            AmountText = (bal * pct).ToString("F8", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Validate address + amount before sending. Returns true if valid.
    /// Same logic as the original code-behind ValidateSendForm().
    /// </summary>
    public bool ValidateAndSend()
    {
        AddressError = "";
        AmountError = "";

        if (string.IsNullOrWhiteSpace(AddressText))
        {
            AddressError = "Address is required";
            RaiseErrorProperties();
            return false;
        }

        if (string.IsNullOrWhiteSpace(AmountText) ||
            !double.TryParse(AmountText, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
        {
            AmountError = "Amount must be greater than 0";
            RaiseErrorProperties();
            return false;
        }

        if (amount <= 0)
        {
            AmountError = "Amount must be greater than 0";
            RaiseErrorProperties();
            return false;
        }

        if (amount < 0.00001)
        {
            AmountError = "Minimum 0.00001 BTC";
            RaiseErrorProperties();
            return false;
        }

        if (double.TryParse(_rawBalance, NumberStyles.Any, CultureInfo.InvariantCulture, out var bal) && amount > bal)
        {
            AmountError = "Amount exceeds balance";
            RaiseErrorProperties();
            return false;
        }

        // Validation passed — show success
        SummaryAmountText = string.IsNullOrEmpty(AmountText)
            ? "0.00000000 BTC"
            : $"{AmountText} BTC";
        IsFormStep = false;
        IsSuccessStep = true;
        return true;
    }

    /// <summary>Close the modal via ShellService.</summary>
    public void Close() => ShellService.HideModal();

    /// <summary>Copy the stub txid to clipboard (needs control ref for clipboard API).</summary>
    public void CopyTxid(Avalonia.Controls.Control control)
        => ClipboardHelper.CopyToClipboard(control, StubTxid);

    private void RaiseErrorProperties()
    {
        this.RaisePropertyChanged(nameof(HasAddressError));
        this.RaisePropertyChanged(nameof(HasAmountError));
    }
}
