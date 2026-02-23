using Angor.Sdk.Funding.Founder;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Portfolio;

public class PortfolioProjectSample : IPortfolioProjectViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IAmountUI Target { get; set; } = new AmountUI(14000);
    public IAmountUI Raised { get; set; } = new AmountUI(63000);
    public IAmountUI InRecovery { get; set; } = new AmountUI(123000);
    public InvestmentStatus InvestmentStatus { get; set; }
    public FounderStatus FounderStatus { get; set; }
    public Uri LogoUri { get; set; } = null!;
    public IEnhancedCommand<Result> CompleteInvestment { get; } = null!;
    public IEnhancedCommand<Result> CancelInvestment { get; } = null!;
    public bool IsInvestmentCompleted { get; set; }
    public IAmountUI Invested { get; } = new AmountUI(123000);
    public IEnhancedCommand GoToManageFunds { get; } = null!;
}