using System.Windows.Input;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Browse.ProjectLookup;

public interface IProjectLookupViewModel
{
    public string? ProjectId { get; set; }
    public IProjectViewModel SelectedProject { get; set; }
    public IObservable<bool> IsBusy { get; }

    ReactiveCommand<string, Maybe<IList<IProjectViewModel>>> Lookup { get; }
    Maybe<IList<IProjectViewModel>> LookupResults { get; }
    public ICommand GoToSelectedProject { get; }
}