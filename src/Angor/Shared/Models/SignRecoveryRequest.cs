namespace Angor.Shared.Models;

public class SignRecoveryRequest
{
    public string ProjectIdentifier { get; set; }

    public string UnfundedReleaseAddress{ get; set; }

    public string UnfundedReleaseKey { get; set; }

    public string InvestmentTransactionHex { get; set; }

    public string AdditionalNpub { get; set; }
}