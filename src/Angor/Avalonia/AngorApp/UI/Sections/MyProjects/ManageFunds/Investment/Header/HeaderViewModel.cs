using AngorApp.Model.ProjectsV2;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Header;

public class HeaderViewModel(IProject project) : IHeaderViewModel
{
    public IProject Project { get; } = project;
}
