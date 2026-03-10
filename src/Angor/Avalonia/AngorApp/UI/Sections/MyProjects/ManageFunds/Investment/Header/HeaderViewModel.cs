using AngorApp.Model.ProjectsV2;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Header;

public class HeaderViewModel(IProject project, IEnhancedCommand refresh) : IHeaderViewModel
{
    public IProject Project { get; } = project;
    public IEnhancedCommand Refresh { get; } = refresh;
}
