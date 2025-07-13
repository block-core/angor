namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class InvestorPositionRecord
{
    public string ProjectIdentifier { get; set; }
    public string InvestmentTransactionHash { get; set; }
    public string InvestorPubKey { get; set; }
    public string UnfundedReleaseAddress { get; set; }
}