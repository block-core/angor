using AngorApp.Sections.Founder.ProjectDetails.MainView.Approve;
using AngorApp.Sections.Founder.ProjectDetails.MainView.Claim;
using AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;
using AngorApp.Sections.Founder.ProjectDetails.Statistics;
using AngorApp.Sections.Portfolio;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView;

public interface IProjectMainViewModel : IProjectViewModel
{
    IReleaseFundsViewModel ReleaseFundsViewModel { get; }
    IClaimFundsViewModel ClaimFundsViewModel { get; }
    IApproveInvestmentsViewModel ApproveInvestmentsViewModel { get; }
    IProjectStatisticsViewModel ProjectStatisticsViewModel { get; }
    ProjectStatus Status { get; }
}