using AngorApp.Sections.Browse.ProjectLookup;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Browse;

public interface IBrowseSectionViewModel
{
    public IEnumerable<IProjectViewModel> Projects { get; }
    public IProjectLookupViewModel ProjectLookupViewModel { get; }
    IEnhancedCommand<Result<List<IProjectViewModel>>> LoadProjects { get; }
    IObservable<bool> IsBusy { get; }
}