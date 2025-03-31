namespace Angor.Client.Models;

public class InvestmentState
{
    public string ProjectIdentifier { get; set; }
    public string InvestmentTransactionHash { get; set; }
    public string InvestorPubKey { get; set; }
    public string UnfundedReleaseAddress { get; set; }

}