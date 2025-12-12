using Angor.Sdk.Funding.Founder;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Approve;

public interface IInvestmentChild
{
    DateTime CreatedOn { get; }
    public string InvestorNostrPubKey { get; }
    public IAmountUI Amount { get; }
    public InvestmentStatus Status { get; set; }
}