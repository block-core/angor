using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio;

public class PortfolioProjectDesign : IPortfolioProject
{
    public string Name { get; set; }
    public string Description { get; set; }
    public IAmountUI Target { get; set; }
    public IAmountUI Raised { get; set; }
    public IAmountUI InRecovery { get; set; }
    public ProjectStatus Status { get; set; }
    public FounderStatus FounderStatus { get; set; }
    public Uri LogoUri { get; set; }
    public IEnhancedCommand<Result> CompleteInvestment { get; }
    public bool IsInvestmentCompleted { get; set; }
}