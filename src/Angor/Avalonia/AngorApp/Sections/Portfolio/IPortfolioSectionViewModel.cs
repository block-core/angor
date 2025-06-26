using Angor.Contexts.Funding.Investor;
using AngorApp.Sections.Portfolio.Items;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio;

public interface IPortfolioSectionViewModel
{
    IReadOnlyCollection<PortfolioItem> Items { get; }
    public IEnumerable<IPortfolioProject> InvestedProjects { get; }
    public IEnhancedCommand<Result<IEnumerable<InvestedProjectDto>>> Load { get; }
    public int FundedProjects { get; }
    public IAmountUI TotalInvested { get; }
    int ProjectsInRecovery { get; }
    IAmountUI RecoveredToPenalty { get; }
}