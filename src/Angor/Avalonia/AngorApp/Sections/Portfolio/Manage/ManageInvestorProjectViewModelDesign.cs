using Angor.Contexts.Funding.Investor.Dtos;

namespace AngorApp.Sections.Portfolio.Manage;

public class ManageInvestorProjectViewModelDesign : IManageInvestorProjectViewModel
{
    public IAmountUI TotalFunds { get; } = new AmountUI(1234000);
    public IEnhancedCommand ViewTransaction { get; } = ReactiveCommand.Create(() => { }).Enhance();
    public IObservable<IEnhancedCommand> Action { get; }
    public DateTime ExpiryDate { get; } = DateTime.Now.AddMonths(1);
    public TimeSpan PenaltyPeriod { get; } = TimeSpan.FromDays(90);

    public IEnumerable<IInvestorProjectItem> Items { get; } = new[]
    {
        new InvestorProjectItemDesign(),
        new InvestorProjectItemDesign(),
        new InvestorProjectItemDesign(),
        new InvestorProjectItemDesign(),
    };

    public IEnhancedCommand Load { get; } = ReactiveCommand.Create(() => Result.Success(new InvestorProjectRecoveryDto())).Enhance();
    public IObservable<RecoveryState> State { get; }
}
