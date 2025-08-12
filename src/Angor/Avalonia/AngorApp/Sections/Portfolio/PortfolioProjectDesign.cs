using Angor.Contexts.Funding.Founder;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio;

public class PortfolioProjectDesign : IPortfolioProject
{
    public string Name { get; set; }
    public string Description { get; set; }
    public IAmountUI Target { get; set; } = new AmountUI(14000);
    public IAmountUI Raised { get; set; } = new AmountUI(63000);
    public IAmountUI InRecovery { get; set; } = new AmountUI(123000);
    public InvestmentStatus InvestmentStatus { get; set; }
    public FounderStatus FounderStatus { get; set; }
    public Uri LogoUri { get; set; }
    public IEnhancedCommand<Result> CompleteInvestment { get; }
    public bool IsInvestmentCompleted { get; set; }
    public IAmountUI Invested { get; } = new AmountUI(123000);
    public IEnhancedCommand GoToManageFunds { get; }
}