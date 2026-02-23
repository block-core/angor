namespace Angor.Sdk.Funding.Investor.Domain;

public class InvestmentRecord
{
    public string ProjectIdentifier { get; set; } = string.Empty;
    public string InvestmentTransactionHash { get; set; } = string.Empty;
    public string? InvestmentTransactionHex { get; set; } //TODO this will be removed in the future, we will scan the wallet for the used UTXOs instead
    public string InvestorPubKey { get; set; } = string.Empty;
    public string UnfundedReleaseAddress { get; set; } = string.Empty;
    
    public DateTime? RequestEventTime { get; set; }
    public string? RequestEventId { get; set; }
    
    public string? RecoveryTransactionId { get; set; }
    public string? RecoveryReleaseTransactionId { get; set; }
    public string? EndOfProjectTransactionId { get; set; }
    
    /// <summary>
    /// The amount invested in satoshis. Stored at publish time so we can sum totals without hitting the indexer.
    /// </summary>
    public long InvestedAmountSats { get; set; }
}