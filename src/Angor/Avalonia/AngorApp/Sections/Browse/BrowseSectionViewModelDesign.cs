using System.Linq;
using AngorApp.Sections.Browse.ProjectLookup;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using CSharpFunctionalExtensions;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Browse;

public class BrowseSectionViewModelDesign : IBrowseSectionViewModel
{
    public BrowseSectionViewModelDesign()
    {
        Projects = SampleData.GetProjects().Select(IProjectViewModel (project) => new ProjectViewModelDesign(project)).ToList();
    }

    public IEnumerable<IProjectViewModel> Projects { get; }
    public IProjectLookupViewModel ProjectLookupViewModel { get; }
    public IEnhancedCommand<Result<IEnumerable<IProjectViewModel>>> LoadProjects { get; }
}