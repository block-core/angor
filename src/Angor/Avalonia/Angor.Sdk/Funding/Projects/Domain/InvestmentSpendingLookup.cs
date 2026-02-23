namespace Angor.Sdk.Funding.Projects.Domain;

public class InvestmentSpendingLookup
{
    public string ProjectIdentifier { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string EndOfProjectTransactionId { get; set; } = string.Empty;
    public string RecoveryTransactionId { get; set; } = string.Empty;
    public string RecoveryReleaseTransactionId { get; set; } = string.Empty;
    public string UnfundedReleaseTransactionId { get; set; } = string.Empty;
    public long AmountInRecovery { get; set; }
}