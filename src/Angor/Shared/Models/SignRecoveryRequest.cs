namespace Angor.Shared.Models;

public class SignRecoveryRequest
{
    public string ProjectIdentifier { get; set; }

    public string InvestorNostrPrivateKey { get; set; }
    public string NostrPubKey { get; set; }
    
    public string InvestmentTransaction { get; set; }

    public string content { get; set; }
}