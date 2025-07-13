using System.Windows.Input;

namespace AngorApp.Sections.Browse;
    

public class ProjectViewModelDesign : IProjectViewModel
{
    public ProjectViewModelDesign()
    {
    }
    
    public ProjectViewModelDesign(IProject project)
    {
        Project = project;
    }

    public IProject Project { get; set; }
    public ICommand GoToDetails { get; set; }
}