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
    public IEnumerable<IPortfolioProjectViewModel> InvestedProjects { get; } = new List<IPortfolioProjectViewModel>();
    public IInvestorStatsViewModel InvestorStats { get; } = new InvestorStatsViewModelDesign
    {
        FundedProjects = 1,
        ProjectsInRecovery = 1,
        TotalInvested = new AmountUI(12345),
        RecoveredToPenalty = new AmountUI(1024),
    };

    public ICommand GoToPenalties { get; }
    public IEnhancedCommand<Result<IWallet>> LoadWallet { get; }
    public IEnhancedCommand<Result<IEnumerable<IPortfolioProjectViewModel>>> LoadPortfolio { get; }
    public IObservable<bool> IsLoading => Observable.Return(false);
}

public class InvestorStatsViewModelDesign : IInvestorStatsViewModel
{
    public int FundedProjects { get; set; }
    public int ProjectsInRecovery { get; set; }
    public IAmountUI TotalInvested { get; set; }
    public IAmountUI RecoveredToPenalty { get; set; }
}

public interface IInvestorStatsViewModel
{
    int FundedProjects { get;  }
    int ProjectsInRecovery { get; }
    IAmountUI TotalInvested { get; }
    IAmountUI RecoveredToPenalty { get; }
}