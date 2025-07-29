using AngorApp.Sections.Founder.ProjectDetails.Investments;
using AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

namespace AngorApp.Sections.Founder.ProjectDetails;

public class FounderProjectDetailsViewModelDesign : IFounderProjectDetailsViewModel
{
    public string Name { get; } = "Test";
    public Uri? BannerUrl { get; set; }
    public string ShortDescription { get; } = "Short description, Bitcoin ONLY.";
    public IProjectInvestmentsViewModel InvestmentsViewModel { get; set; } = new ProjectInvestmentsViewModelDesign(); 
    public IManageFundsViewModel ManageFundsViewModel { get; set; } = new ManageFundsViewModelDesign();
}