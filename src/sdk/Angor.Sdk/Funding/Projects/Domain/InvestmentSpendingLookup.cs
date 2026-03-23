namespace Angor.Sdk.Funding.Projects.Domain;

public class InvestmentSpendingLookup
{
    public string ProjectIdentifier { get; set; }
    public string TransactionId { get; set; }
    public string EndOfProjectTransactionId { get; set; }
    public string RecoveryTransactionId { get; set; }
    public string RecoveryReleaseTransactionId { get; set; }
    public string UnfundedReleaseTransactionId { get; set; }
    public long AmountInRecovery { get; set; }
}