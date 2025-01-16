namespace Angor.Shared.Models;

public class SignRecoveryRequest
{
    public string ProjectIdentifier { get; set; }

    /// <summary>
    /// An address that will be used to release the funds
    /// to the investor in case the target amount is not reached.
    /// </summary>
    public string ReleaseAddress{ get; set; }

    /// <summary>
    /// A pubkey that will be converted by the founder to an address that will be used
    /// to release the funds to the investor in case the target amount is not reached.
    /// </summary>
    public string ReleaseKey { get; set; }

    public string InvestmentTransactionHex { get; set; }
}