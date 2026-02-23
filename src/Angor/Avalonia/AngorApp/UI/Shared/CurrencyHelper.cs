using AngorApp.Model.Amounts;

namespace AngorApp.UI.Shared;

/// <summary>
/// Provides static currency symbol access for AXAML bindings via x:Static.
/// The symbol is derived from the network configuration (e.g. "BTC" for mainnet, "TBTC" for testnet).
/// </summary>
public static class CurrencyHelper
{
    public static string Symbol => AmountUI.DefaultSymbol;
    public static string AmountLabel => $"Amount ({AmountUI.DefaultSymbol})";
    public static string TargetAmountLabel => $"Target Amount ({AmountUI.DefaultSymbol}) *";
    public static string GoalLabel => $"Goal ({AmountUI.DefaultSymbol}) *";
    public static string BitcoinLabel => $"Bitcoin ({AmountUI.DefaultSymbol})";
    public static string MinInvestmentHint => $"Minimum investment: 0.001 {AmountUI.DefaultSymbol}";
    public static string CurrencyDisplayHint => $"Bitcoin-only application - currency display is fixed to {AmountUI.DefaultSymbol} or sats.";
}
