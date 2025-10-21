using AngorApp.Sections.Browse.Details;
using AngorApp.Sections.Founder.ProjectDetails.MainView.Approve;
using AngorApp.Sections.Founder.ProjectDetails.MainView.Claim;
using AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;
using AngorApp.Sections.Founder.ProjectDetails.Statistics;
using AngorApp.Sections.Portfolio;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView;

public class ProjectMainViewModelDesign : IProjectMainViewModel
{
    public string Name { get; } = "Sample Project Name";
    public string ShortDescription { get; } = "This is a sample project description for design purposes.";
    public Uri? Banner { get; } = new Uri("https://images-assets.nasa.gov/image/PIA14417/PIA14417~thumb.jpg?as=webp");
    public Uri? Avatar { get; } = new Uri("https://images-assets.nasa.gov/image/GSFC_20171208_Archive_e001518/GSFC_20171208_Archive_e001518~thumb.jpg?as=webp");
    public IReleaseFundsViewModel ReleaseFundsViewModel { get; } = new ReleaseFundsViewModelDesign();
    public IClaimFundsViewModel ClaimFundsViewModel { get; } = new ClaimFundsViewModelDesign();
    public IApproveInvestmentsViewModel ApproveInvestmentsViewModel { get; } = new ApproveInvestmentsViewModelDesign();
    public IFullProject ProjectStatisticsViewModel { get; set; } = new FullProjectDesign();
    public ProjectStatus Status { get; set; } = ProjectStatus.Started;
}