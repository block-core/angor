using Angor.Contexts.Funding.Investor.Dtos;

namespace AngorApp.Sections.Portfolio.Manage;

public interface IManageInvestorProjectViewModel
{
    IAmountUI TotalFunds { get; }
    IEnhancedCommand ViewTransaction { get; }
    DateTime ExpiryDate { get; }
    TimeSpan PenaltyPeriod { get; }
    IEnumerable<IInvestorProjectItem> Items { get; }
    IInvestedProject Project { get; }

    IEnhancedCommand Load { get; }
}