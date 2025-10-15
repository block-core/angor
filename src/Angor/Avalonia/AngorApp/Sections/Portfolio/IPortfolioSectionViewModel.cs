using System.Windows.Input;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Investor;
using AngorApp.Sections.Portfolio.Items;

namespace AngorApp.Sections.Portfolio;

public interface IPortfolioSectionViewModel
{
    public ICollection<IPortfolioProjectViewModel> InvestedProjects { get; }
    public IInvestorStatsViewModel InvestorStats { get; }
    public ICommand GoToPenalties { get; }
    IEnhancedCommand<Result<ICollection<IPortfolioProjectViewModel>>> LoadPortfolio { get; }
    public IObservable<bool> IsLoading { get; }
}