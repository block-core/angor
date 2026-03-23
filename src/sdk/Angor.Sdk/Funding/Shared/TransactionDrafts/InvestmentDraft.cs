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
}