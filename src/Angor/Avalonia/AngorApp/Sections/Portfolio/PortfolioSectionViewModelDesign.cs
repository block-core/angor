using Angor.Contexts.Funding.Investor;
using AngorApp.Sections.Portfolio.Items;
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