namespace Angor.Sdk.Funding.Founder.Domain;

public class SignatureReleaseItem
{
    public string investorNostrPubKey = string.Empty;
    public DateTime InvestmentRequestTime { get; set; }
    public string EncryptedSignRecoveryMessage { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public DateTime ApprovaleTime { get; set; }
    public DateTime ReleaseSignaturesTime { get; set; }
            
    public string? SignRecoveryRequestEventId { get; set; }
}