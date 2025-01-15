namespace Angor.Shared.Models;

public class SignRecoveryRequest
{
    public string ProjectIdentifier { get; set; }

    public string InvestorNostrPrivateKey { get; set; }
    public string NostrPubKey { get; set; }

    /// <summary>
    /// An address that will be used to release the funds to the investor in case the target amount is not reached.
    /// </summary>
    public string ReleaseAddress{ get; set; }

    public string InvestmentTransaction { get; set; }

    public string EncryptedContent { get; set; }
}