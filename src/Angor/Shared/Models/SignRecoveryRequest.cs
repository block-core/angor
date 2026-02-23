namespace Angor.Shared.Models;

public class SignRecoveryRequest
{
    public string ProjectIdentifier { get; set; } = string.Empty;

    public string UnfundedReleaseAddress{ get; set; } = string.Empty;

    public string UnfundedReleaseKey { get; set; } = string.Empty;

    public string InvestmentTransactionHex { get; set; } = string.Empty;

    public string AdditionalNpub { get; set; } = string.Empty;
}