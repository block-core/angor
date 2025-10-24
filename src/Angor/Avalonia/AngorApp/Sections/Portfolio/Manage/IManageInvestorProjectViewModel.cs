namespace AngorApp.Sections.Portfolio.Manage;

public interface IManageInvestorProjectViewModel
{
    IAmountUI TotalFunds { get; }
    IEnhancedCommand ViewTransaction { get; }
    IObservable<IEnhancedCommand> Action { get; }
    DateTime ExpiryDate { get; }
    TimeSpan PenaltyPeriod { get; }
    IEnumerable<IInvestorProjectItem> Items { get; }
    IInvestedProject Project { get; }
    IEnhancedCommand Load { get; }
}
