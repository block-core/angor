using System.Windows.Input;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Investor;
using AngorApp.UI.Sections.Portfolio.Items;

namespace AngorApp.UI.Sections.Portfolio;

public interface IPortfolioSectionViewModel
{
    public ICollection<IPortfolioProjectViewModel> InvestedProjects { get; }
    public IInvestorStatsViewModel InvestorStats { get; }
    public ICommand GoToPenalties { get; }
    IEnhancedCommand<Result<ICollection<IPortfolioProjectViewModel>>> LoadPortfolio { get; }
    public IObservable<bool> IsLoading { get; }
}