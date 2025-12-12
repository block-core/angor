using System.Windows.Input;
using Angor.Sdk.Funding.Founder.Dtos;
using AngorApp.UI.Sections.Portfolio.Items;

namespace AngorApp.UI.Sections.Portfolio;

public class PortfolioSectionViewModelSample : IPortfolioSectionViewModel
{
    public PortfolioSectionViewModelSample()
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
    public ICollection<IPortfolioProjectViewModel> InvestedProjects { get; } = new List<IPortfolioProjectViewModel>();
    public IInvestorStatsViewModel InvestorStats { get; } = new InvestorStatsViewModelSample
    {
        FundedProjects = 1,
        ProjectsInRecovery = 1,
        TotalInvested = new AmountUI(12345),
        RecoveredToPenalty = new AmountUI(1024),
    };

    public ICommand GoToPenalties { get; }
    public IEnhancedCommand<Result<IWallet>> LoadWallet { get; }
    public IEnhancedCommand<Result<ICollection<IPortfolioProjectViewModel>>> LoadPortfolio { get; }
    public IObservable<bool> IsLoading => Observable.Return(false);
}

public class InvestorStatsViewModelSample : IInvestorStatsViewModel
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