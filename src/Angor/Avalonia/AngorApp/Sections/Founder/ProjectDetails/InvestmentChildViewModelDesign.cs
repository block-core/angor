using Angor.Contexts.Funding.Founder;

namespace AngorApp.Sections.Founder.ProjectDetails;

public class InvestmentChildViewModelDesign : IInvestmentChild
{
    public IAmountUI Amount { get; set; } = new AmountUI(1000);
    public string InvestorNostrPubKey { get; set; } = "npub1test1234567890test1234567890test1234567890test1234567890test1234567890";
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public InvestmentStatus Status { get; set; } = InvestmentStatus.Invested;
}