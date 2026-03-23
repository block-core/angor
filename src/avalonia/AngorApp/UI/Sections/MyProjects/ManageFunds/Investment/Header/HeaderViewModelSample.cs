using AngorApp.Model.Funded.Investment.Samples;
using AngorApp.Model.ProjectsV2;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Header;

public class HeaderViewModelSample : IHeaderViewModel
{
    public IProject Project { get; set; } = new InvestmentProjectSample();
    public IEnhancedCommand Refresh => Project.Refresh;
}
