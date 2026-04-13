namespace App.UI.Shared;

/// <summary>
/// Shared constants replacing magic numbers and strings scattered across ViewModels.
/// </summary>
public static class Constants
{
    /// <summary>Default miner fee estimate in BTC (used when dynamic fee estimation is unavailable).</summary>
    public const double MinerFee = 0.00000391;

    /// <summary>Angor fee rate (1%).</summary>
    public const double AngorFeeRate = 0.01;

    /// <summary>Angor fee percentage for display.</summary>
    public const string AngorFeeDisplay = "1%";

    /// <summary>Placeholder for Lightning invoice — not yet supported.</summary>
    public const string InvoiceString = "Lightning invoices coming soon";

    /// <summary>Minimum investment amount in BTC.</summary>
    public const double MinInvestmentAmount = 0.001;
}
