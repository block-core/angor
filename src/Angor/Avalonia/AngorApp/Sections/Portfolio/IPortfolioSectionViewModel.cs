using System.Windows.Input;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Investor;
using AngorApp.Sections.Portfolio.Items;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio;

public interface IPortfolioSectionViewModel
{
    public IEnumerable<IPortfolioProject> InvestedProjects { get; }
    public InvestorStatsViewModel InvestorStats { get; }
    public ICommand GoToPenalties { get; }
    IEnhancedCommand<Result<IWallet>> LoadWallet { get; }
    IEnhancedCommand<Result<InvestorStatsViewModel>> LoadStats { get; }
    IEnhancedCommand<Result<IEnumerable<IPortfolioProject>>> LoadPortfolio { get; }
}