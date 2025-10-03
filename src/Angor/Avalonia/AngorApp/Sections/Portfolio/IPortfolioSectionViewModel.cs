using System.Windows.Input;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Investor;
using AngorApp.Sections.Portfolio.Items;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio;

public interface IPortfolioSectionViewModel
{
    public IEnumerable<IPortfolioProjectViewModel> InvestedProjects { get; }
    public IInvestorStatsViewModel InvestorStats { get; }
    public ICommand GoToPenalties { get; }
    IEnhancedCommand<Result<IWallet>> LoadWallet { get; }
    IEnhancedCommand<Result<IEnumerable<IPortfolioProjectViewModel>>> LoadPortfolio { get; }
    public IObservable<bool> IsLoading { get; }
}