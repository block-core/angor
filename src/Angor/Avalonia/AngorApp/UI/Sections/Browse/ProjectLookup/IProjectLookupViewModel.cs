using System.Windows.Input;
using AngorApp.Core;

namespace AngorApp.UI.Sections.Browse.ProjectLookup;

public interface IProjectLookupViewModel : IDisposable
{
    public string? ProjectId { get; set; }
    public IProjectViewModel SelectedProject { get; set; }
    public IObservable<bool> IsBusy { get; }

    ReactiveCommand<string, SafeMaybe<IList<IProjectViewModel>>> Lookup { get; }
    SafeMaybe<IList<IProjectViewModel>> LookupResults { get; }
    public ICommand GoToSelectedProject { get; }
}