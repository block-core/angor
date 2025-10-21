using Angor.Contexts.Funding.Founder;
using Angor.UI.Model.Implementation.Projects;
using AngorApp.Sections.Founder.ProjectDetails.MainView.Approve;
using AngorApp.Sections.Founder.ProjectDetails.MainView.Claim;
using AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;
using AngorApp.Sections.Founder.ProjectDetails.Statistics;
using AngorApp.Sections.Portfolio;
using AngorApp.UI.Services;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView;

public class ProjectMainViewModel : IProjectMainViewModel
{
    private readonly IFullProject project;

    public ProjectMainViewModel(IFullProject project, IFounderAppService founderAppService, UIServices uiServices, IWalletContext walletContext)
    {
        this.project = project;
        Status = project.Status;
        ReleaseFundsViewModel = new ReleaseFundsViewModel(project, founderAppService, walletContext, uiServices);
        ClaimFundsViewModel = new ClaimFundsViewModel(project, founderAppService, uiServices, walletContext);
        ApproveInvestmentsViewModel = new ApproveInvestmentsViewModel(project, founderAppService, uiServices, walletContext);
        ProjectStatisticsViewModel = project;
    }

    public IFullProject ProjectStatisticsViewModel { get; }
    public ProjectStatus Status { get; }
    public Uri? Avatar => project.Avatar;
    public string Name => project.Name;
    public string ShortDescription => project.ShortDescription;
    public Uri? Banner => project.Banner;
    public IReleaseFundsViewModel ReleaseFundsViewModel { get; }
    public IClaimFundsViewModel ClaimFundsViewModel { get; }
    public IApproveInvestmentsViewModel ApproveInvestmentsViewModel { get; }
}
