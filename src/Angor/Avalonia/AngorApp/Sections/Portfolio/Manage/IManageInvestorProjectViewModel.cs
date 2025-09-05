using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio.Manage;

public interface IManageInvestorProjectViewModel
{
    public IAmountUI TotalFunds { get; }
    public IEnhancedCommand ViewTransaction { get; }
    public DateTime ExpiryDate { get; }
    public TimeSpan PenaltyPeriod { get; }
    public IEnumerable<IInvestorProjectItem> Items { get; }
    public IInvestedProject Project { get; }
}

public interface IInvestedProject
{
    public IAmountUI TotalFunds { get; }
    public DateTime ExpiryDate { get; }
    public TimeSpan PenaltyPeriod { get; }
    public string Name { get; }
}