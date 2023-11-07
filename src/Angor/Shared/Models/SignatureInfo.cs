namespace Angor.Shared.Models;

public class SignatureInfo
{
    public string ProjectIdentifier { get; set; }
    public string TransactionId { get; set; }
    public string RecoveryTransactionId { get; set; }
    public string RecoveryReleaseTransactionId { get; set; }
    public string EndOfProjectTransactionId { get; set; }
    public List<SignatureInfoItem> Signatures { get; set; } = new();
}