using System.Linq;

namespace AngorApp.UI.Sections.Portfolio;

public class InvestorStatsViewModel : IInvestorStatsViewModel
{
    public InvestorStatsViewModel(ICollection<IPortfolioProjectViewModel> projects)
    {
        TotalInvested = new AmountUI(projects.Sum(project => project.Invested.Sats));
        RecoveredToPenalty = new AmountUI(projects.Sum(project => project.InRecovery.Sats));
        ProjectsInRecovery = projects.Count(project => project.InRecovery.Sats > 0);
        FundedProjects = projects.Count;
    }

    public IAmountUI TotalInvested { get; }
    public IAmountUI RecoveredToPenalty { get; }
    public int ProjectsInRecovery { get; }
    public int FundedProjects { get; }
}