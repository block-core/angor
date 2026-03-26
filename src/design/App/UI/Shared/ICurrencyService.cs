namespace App.UI.Shared;

/// <summary>
/// Provides the current network's currency symbol and pre-composed labels.
/// The symbol is derived from <see cref="Angor.Shared.INetworkConfiguration"/> (e.g. "BTC" for mainnet, "TBTC" for testnet, "LBTC" for Liquid).
/// Inject this service into ViewModels and code-behind instead of hardcoding "BTC".
/// </summary>
public interface ICurrencyService
{
    /// <summary>Current network ticker (e.g. "BTC", "TBTC", "LBTC").</summary>
    string Symbol { get; }

    /// <summary>Format a BTC-denominated value with the network symbol, e.g. "0.00100000 TBTC".</summary>
    string FormatBtc(double btcValue, string format = "F8");

    /// <summary>e.g. "Amount (TBTC)"</summary>
    string AmountLabel { get; }

    /// <summary>e.g. "Target Amount (TBTC) *"</summary>
    string TargetAmountLabel { get; }

    /// <summary>e.g. "Goal (TBTC) *"</summary>
    string GoalLabel { get; }

    /// <summary>e.g. "Minimum investment: 0.001 TBTC"</summary>
    string MinInvestmentHint { get; }

    /// <summary>e.g. "~0.00000391 TBTC"</summary>
    string MinerFeeDisplay { get; }

    /// <summary>e.g. "Price per period (TBTC) *"</summary>
    string PricePerPeriodLabel { get; }
}
