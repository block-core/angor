using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio.Manage;

public class ManageInvestorProjectViewModelDesign : IManageInvestorProjectViewModel
{
    public IAmountUI TotalFunds { get; } = new AmountUI(12345);
    public IEnhancedCommand ViewTransaction { get; } = ReactiveCommand.Create(() => {}).Enhance();
    public DateTime ExpiryDate { get; } = DateTime.Now.AddMonths(1);
    public TimeSpan PenaltyPeriod { get; } = TimeSpan.FromDays(90);
}