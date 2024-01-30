namespace Angor.Shared.Models;

public class SignatureInfo
{
    public string ProjectIdentifier { get; set; }
    public string TransactionId { get; set; }
    public string RecoveryTransactionId { get; set; }
    public string RecoveryReleaseTransactionId { get; set; }
    public string EndOfProjectTransactionId { get; set; }
    public List<SignatureInfoItem> Signatures { get; set; } = new();
    public DateTime? TimeOfRequestForSigning { get; set; }
    public string? SignatureRequestEventId { get; set; }

    public string? SignedTransactionHex { get; set; }
    public long AmountInvested { get; set; }
    public long AmountInRecovery { get; set; }
}