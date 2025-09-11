using Angor.Contexts.Funding.Investor.Dtos;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio.Manage;

public class ManageInvestorProjectViewModelDesign : IManageInvestorProjectViewModel
{
    public IAmountUI TotalFunds { get; } = new AmountUI(1234000);
    public IEnhancedCommand ViewTransaction { get; } = ReactiveCommand.Create(() => { }).Enhance();
    public DateTime ExpiryDate { get; } = DateTime.Now.AddMonths(1);
    public TimeSpan PenaltyPeriod { get; } = TimeSpan.FromDays(90);

    public IEnumerable<IInvestorProjectItem> Items { get; } = new[]
    {
        new InvestorProjectItemDesign(),
        new InvestorProjectItemDesign(),
        new InvestorProjectItemDesign(),
        new InvestorProjectItemDesign(),
    };

    public IInvestedProject Project { get; } = new InvestedProjectDesign();

    public IEnhancedCommand<Result<InvestorProjectRecoveryDto>> Load { get; } = ReactiveCommand.Create(() => Result.Success(new InvestorProjectRecoveryDto())).Enhance();
}