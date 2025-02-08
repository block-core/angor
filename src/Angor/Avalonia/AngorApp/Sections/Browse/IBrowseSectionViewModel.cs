using System.Windows.Input;
using AngorApp.Sections.Browse.Details;
using AngorApp.Sections.Browse.ProjectLookup;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Browse;

public interface IBrowseSectionViewModel
{
    public IList<IProjectViewModel> Projects { get; }
    ReactiveCommand<Unit, Unit> OpenHub { get; set; }
    public IProjectLookupViewModel ProjectLookupViewModel { get; }
}