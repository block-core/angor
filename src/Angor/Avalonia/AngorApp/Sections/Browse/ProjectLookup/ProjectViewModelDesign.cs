using System.Windows.Input;
using AngorApp.Model;

namespace AngorApp.Sections.Browse.ProjectLookup;

public class ProjectViewModelDesign(IProject project) : IProjectViewModel
{
    public IProject Project { get; } = project;
    public ICommand GoToDetails { get; set; }
}