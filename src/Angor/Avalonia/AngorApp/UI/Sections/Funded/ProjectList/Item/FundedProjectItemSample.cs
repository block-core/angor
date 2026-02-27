using AngorApp.UI.Sections.Shared;
using Angor.Sdk.Funding.Founder;
using AngorApp.UI.Sections.Shared.Project;

namespace AngorApp.UI.Sections.Funded.ProjectList.Item;

public class FundedProjectItemSample : IFundedProjectItem
{
    public IProject Project { get; set; } = new InvestmentProjectSample();

    public IInvestmentItem Investment { get; } = new InvestmentSample()
    {
        Amount = Observable.Return(new AmountUI(10000000)), Date = DateTimeOffset.Now, Status = Observable.Return(InvestmentStatus.Invested)
    };

    public IEnhancedCommand Manage { get; } = EnhancedCommand.Create(() => { });
}