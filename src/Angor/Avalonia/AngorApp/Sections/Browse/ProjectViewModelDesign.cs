using System.Windows.Input;
using Zafiro.UI.Commands;

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
    public IEnhancedCommand GoToDetails { get; set; }
}