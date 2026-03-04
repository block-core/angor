using Angor.Sdk.Common;

namespace Angor.Sdk.Funding.Shared.TransactionDrafts;

public record InvestmentDraft(string InvestorKey) : TransactionDraft
{
    public Amount MinerFee { get; set; } = new Amount(-1);
    public Amount AngorFee { get; set; } = new Amount(-1);
    
    /// <summary>
    /// The investment amount in satoshis (excluding fees).
    /// </summary>
    public Amount InvestedAmount { get; set; } = new Amount(0);

    /// <summary>
    /// The index used to derive the investor key, allowing the same investor to invest multiple times in the same project.
    /// </summary>
    public int InvestmentIndex { get; set; } = 0;
}