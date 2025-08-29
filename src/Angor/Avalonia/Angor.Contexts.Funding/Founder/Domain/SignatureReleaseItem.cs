namespace Angor.Contexts.Funding.Founder.Domain;

public class SignatureReleaseItem
{
    public string investorNostrPubKey;
    public DateTime InvestmentRequestTime { get; set; }
    public string EncryptedSignRecoveryMessage { get; set; }
    public string EventId { get; set; }
    public DateTime ApprovaleTime { get; set; }
    public DateTime ReleaseSignaturesTime { get; set; }
            
    public string? SignRecoveryRequestEventId { get; set; }
}