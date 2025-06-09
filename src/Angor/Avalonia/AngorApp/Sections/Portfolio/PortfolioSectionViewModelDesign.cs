using Angor.Contexts.Funding.Investor;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio;

public class PortfolioSectionViewModelDesign : IPortfolioSectionViewModel
{
    public PortfolioSectionViewModelDesign()
    {
        Items =
        [
            new PortfolioItem("Ariton", "0"),
            new PortfolioItem("Total invested", "0 TBTC"),
            new PortfolioItem("Wallet", "0 TBTC"),
            new PortfolioItem("In Recovery", "0 TBTC"),
        ];
    }

    public IReadOnlyCollection<PortfolioItem> Items { get; }
    public IEnumerable<IPortfolioProject> InvestedProjects { get; } = new List<IPortfolioProject>();
    public IEnhancedCommand<Result<IEnumerable<InvestedProjectDto>>> Load { get; }
}

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
}