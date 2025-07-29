using Angor.Contexts.Funding.Projects.Application.Dtos;
using AngorApp.Sections.Founder.ProjectDetails.Investments;
using AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

namespace AngorApp.Sections.Founder.ProjectDetails;

public interface IFounderProjectDetailsViewModel
{
    public string Name { get; }
    public Uri? BannerUrl { get; }
    public string ShortDescription { get; }
    public IProjectInvestmentsViewModel InvestmentsViewModel { get; }
    public IManageFundsViewModel ManageFundsViewModel { get; }
    public bool HasProjectStarted { get; }
    public ProjectDto Project { get; }
}