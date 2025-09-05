using Angor.Contexts.Funding.Investor.Dtos;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio.Manage;

public interface IManageInvestorProjectViewModel
{
    IAmountUI TotalFunds { get; }
    IEnhancedCommand ViewTransaction { get; }
    DateTime ExpiryDate { get; }
    TimeSpan PenaltyPeriod { get; }
    IEnumerable<IInvestorProjectItem> Items { get; }
    IInvestedProject Project { get; }

    IEnhancedCommand<Result<InvestorProjectRecoveryDto>> Load { get; }
}