using AngorApp.Sections.Browse.ProjectLookup;

namespace AngorApp.Sections.Browse;

public interface IBrowseSectionViewModel
{
    public ICollection<IProjectViewModel> Projects { get; }
    public IProjectLookupViewModel ProjectLookupViewModel { get; }
    IEnhancedCommand<Result<IEnumerable<IProjectViewModel>>> LoadProjects { get; }
}