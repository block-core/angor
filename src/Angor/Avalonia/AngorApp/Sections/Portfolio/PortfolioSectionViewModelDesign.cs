using System.Windows.Input;
using Angor.Contexts.Funding.Founder.Dtos;
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
    public InvestorStatsViewModel InvestorStats { get; } = new(new InvestorStatsDto()
    {
        FundedProjects = 1,
        ProjectsInRecovery = 1,
        TotalInvested = 1234,
        RecoveredToPenalty = 0023,
    });

    public ICommand GoToPenalties { get; }

    public IEnhancedCommand<Result<IWallet>> LoadWallet { get; }
    public IEnhancedCommand<Result<InvestorStatsViewModel>> LoadStats { get; }
    public IEnhancedCommand<Result<IEnumerable<IPortfolioProject>>> LoadPortfolio { get; }
}