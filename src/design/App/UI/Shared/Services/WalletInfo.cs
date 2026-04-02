using System.Globalization;
using Angor.Sdk.Common;

namespace App.UI.Shared.Services;

/// <summary>
/// Unified wallet representation replacing WalletSwitcherItem, WalletItem, and WalletItemViewModel.
/// Carries identity + balance data so every consumer gets consistent, up-to-date information.
/// </summary>
public partial class WalletInfo : ReactiveObject
{
    public WalletId Id { get; }
    public string Name { get; }

    /// <summary>Currency symbol used for formatted display properties (e.g. "BTC", "TBTC").</summary>
    public string CurrencySymbol { get; }

    /// <summary>Wallet type label for display (e.g. "On-Chain").</summary>
    public string WalletType { get; } = "On-Chain";

    [Reactive] private long totalBalanceSats;
    [Reactive] private long unconfirmedBalanceSats;
    [Reactive] private long reservedBalanceSats;
    [Reactive] private bool isSelected;

    public WalletInfo(WalletId id, string name, string currencySymbol)
    {
        Id = id;
        Name = name;
        CurrencySymbol = currencySymbol;

        // Raise property changed for computed display properties when balances change
        this.WhenAnyValue(x => x.TotalBalanceSats, x => x.UnconfirmedBalanceSats, x => x.ReservedBalanceSats)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(AvailableSats));
                this.RaisePropertyChanged(nameof(Balance));
                this.RaisePropertyChanged(nameof(PendingBalance));
                this.RaisePropertyChanged(nameof(ReservedBalance));
                this.RaisePropertyChanged(nameof(HasPendingBalance));
                this.RaisePropertyChanged(nameof(HasReservedBalance));
            });
    }

    /// <summary>Confirmed + unconfirmed balance in sats.</summary>
    public long AvailableSats => TotalBalanceSats + UnconfirmedBalanceSats;

    // ── Bindable display properties (AXAML-friendly) ──

    /// <summary>Full-precision confirmed balance with symbol, e.g. "0.01000000 BTC". Bindable.
    /// Uses TotalBalanceSats (confirmed only) because PendingBalance displays unconfirmed separately.</summary>
    public string Balance
    {
        get
        {
            var btc = (double)TotalBalanceSats.ToUnitBtc();
            return $"{btc:F8} {CurrencySymbol}";
        }
    }

    /// <summary>Formatted pending balance or empty string when zero. Bindable.</summary>
    public string PendingBalance
    {
        get
        {
            if (UnconfirmedBalanceSats == 0) return "";
            return $"{UnconfirmedBalanceSats.ToUnitBtc():F8} {CurrencySymbol}";
        }
    }

    /// <summary>Formatted reserved balance or empty string when zero. Bindable.</summary>
    public string ReservedBalance
    {
        get
        {
            if (ReservedBalanceSats == 0) return "";
            return $"{ReservedBalanceSats.ToUnitBtc():F8} {CurrencySymbol}";
        }
    }

    public bool HasPendingBalance => UnconfirmedBalanceSats != 0;
    public bool HasReservedBalance => ReservedBalanceSats != 0;

    // ── Method-based formatting (for non-AXAML callers like ShellViewModel) ──

    /// <summary>Formatted balance string, e.g. "0.0100 BTC".</summary>
    public string FormattedBalance(string currencySymbol)
    {
        var btc = AvailableSats.ToUnitBtc();
        return btc.ToString("F4", CultureInfo.InvariantCulture) + " " + currencySymbol;
    }

    /// <summary>Formatted full-precision balance, e.g. "0.01000000 BTC".</summary>
    public string FormattedBalanceFull(string currencySymbol)
    {
        var btc = (double)AvailableSats.ToUnitBtc();
        return $"{btc:F8} {currencySymbol}";
    }
}
