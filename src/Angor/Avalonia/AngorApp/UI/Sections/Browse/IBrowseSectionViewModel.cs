using AngorApp.UI.Sections.Browse.ProjectLookup;

namespace AngorApp.UI.Sections.Browse;

public interface IBrowseSectionViewModel
{
    public ICollection<IProjectViewModel> Projects { get; }
    public IProjectLookupViewModel ProjectLookupViewModel { get; }
    IEnhancedCommand<Result<IEnumerable<IProjectViewModel>>> LoadProjects { get; }
}