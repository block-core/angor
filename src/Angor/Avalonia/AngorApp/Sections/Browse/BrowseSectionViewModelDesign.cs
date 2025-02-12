using System.Linq;
using AngorApp.Sections.Browse.ProjectLookup;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Browse;

public class BrowseSectionViewModelDesign : IBrowseSectionViewModel
{
    public BrowseSectionViewModelDesign()
    {
        Projects = SampleData.GetProjects().Select(IProjectViewModel (project) => new ProjectViewModelDesign(project)).ToList();
    }

    public IList<IProjectViewModel> Projects { get; }
    public ReactiveCommand<Unit, Unit> OpenHub { get; set; }
    public string? ProjectId { get; set; }
    public IObservable<bool> IsBusy { get; set; }
    public IProjectLookupViewModel ProjectLookupViewModel { get; }
}