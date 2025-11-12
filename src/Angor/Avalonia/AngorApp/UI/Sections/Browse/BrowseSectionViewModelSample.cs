using System.Linq;
using AngorApp.UI.Sections.Browse.ProjectLookup;

namespace AngorApp.UI.Sections.Browse;

public class BrowseSectionViewModelSample : IBrowseSectionViewModel
{
    public BrowseSectionViewModelSample()
    {
        Projects = SampleData.GetProjects().Select(IProjectViewModel (project) => new ProjectViewModelSample(project)).ToList();
    }

    public ICollection<IProjectViewModel> Projects { get; }
    public IProjectLookupViewModel ProjectLookupViewModel { get; }
    public IEnhancedCommand<Result<IEnumerable<IProjectViewModel>>> LoadProjects { get; }
}