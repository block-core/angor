using System.Linq;
using System.Windows.Input;
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
        
        Load = ReactiveCommand.Create(() => Result.Success(Enumerable.Empty<InvestedProjectDto>())).Enhance();
        GoToPenalties = ReactiveCommand.Create(() => { });
    }

    public IReadOnlyCollection<PortfolioItem> Items { get; }
    public IEnumerable<IPortfolioProject> InvestedProjects { get; } = new List<IPortfolioProject>();
    public IEnhancedCommand<Result<IEnumerable<InvestedProjectDto>>> Load { get; }
    public int FundedProjects { get; set; }
    public IAmountUI TotalInvested { get; set; }
    public int ProjectsInRecovery { get; set; }
    public IAmountUI RecoveredToPenalty { get; set; }
    public ICommand GoToPenalties { get; }
}