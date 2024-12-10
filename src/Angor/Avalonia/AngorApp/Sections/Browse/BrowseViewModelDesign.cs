using System.Linq;
using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Sections.Browse;

public class BrowseViewModelDesign : IBrowseViewModel
{
    public BrowseViewModelDesign()
    {
        Projects = SampleData.GetProjects().Select(project => new ProjectViewModel(project, null)).ToList();
    }
    
    public IReadOnlyCollection<ProjectViewModel> Projects { get; set; }
    public ReactiveCommand<Unit, Unit> OpenHub { get; set; }
}