using Angor.Sdk.Funding.Founder;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Approve;

public class InvestmentChildViewModelSample : IInvestmentChild
{
    public IAmountUI Amount { get; set; } = new AmountUI(1000);
    public string InvestorNostrPubKey { get; set; } = "npub1test1234567890test1234567890test1234567890test1234567890test1234567890";
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public InvestmentStatus Status { get; set; } = InvestmentStatus.Invested;
}