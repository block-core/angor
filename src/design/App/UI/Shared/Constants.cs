namespace App.UI.Shared;

/// <summary>
/// Shared constants replacing magic numbers and strings scattered across ViewModels.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Vue threshold: investments below this amount (in BTC) are auto-approved.
    /// Vue ref: handleInvestment() in App.vue — amount &lt; 0.01 → auto-approved.
    /// </summary>
    public const double AutoApprovalThreshold = 0.01;

    /// <summary>Default miner fee estimate in BTC (used when dynamic fee estimation is unavailable).</summary>
    public const double MinerFee = 0.00000391;

    /// <summary>Angor fee rate (1%).</summary>
    public const double AngorFeeRate = 0.01;

    /// <summary>Default miner fee estimate for display (used when dynamic fee estimation is unavailable).</summary>
    public const string MinerFeeDisplay = "~0.00000391 BTC";

    /// <summary>Angor fee percentage for display.</summary>
    public const string AngorFeeDisplay = "1%";

    /// <summary>Placeholder for Lightning invoice — not yet supported.</summary>
    public const string InvoiceString = "Lightning invoices coming soon";

    /// <summary>Minimum investment amount in BTC.</summary>
    public const double MinInvestmentAmount = 0.001;
}
