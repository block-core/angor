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
    private readonly FullProject project;

    public ProjectMainViewModel(FullProject project, IFounderAppService founderAppService, UIServices uiServices, IWalletContext walletContext)
    {
        this.project = project;
        Status = project.Status;
        ReleaseFundsViewModel = new ReleaseFundsViewModel(project.Info.Id, founderAppService, walletContext, uiServices);
        ClaimFundsViewModel = new ClaimFundsViewModel(project.Info.Id, founderAppService, uiServices, walletContext);
        ApproveInvestmentsViewModel = new ApproveInvestmentsViewModel(project.Info.Id, founderAppService, uiServices, walletContext);
        ProjectStatisticsViewModel = new ProjectStatisticsViewModel(project);
    }

    public IProjectStatisticsViewModel ProjectStatisticsViewModel { get; }
    public ProjectStatus Status { get; }
    public Uri? Avatar => project.Info.Avatar;
    public string Name => project.Info.Name;
    public string ShortDescription => project.Info.ShortDescription;
    public Uri? Banner => project.Info.Banner;
    public IReleaseFundsViewModel ReleaseFundsViewModel { get; }
    public IClaimFundsViewModel ClaimFundsViewModel { get; }
    public IApproveInvestmentsViewModel ApproveInvestmentsViewModel { get; }
}
