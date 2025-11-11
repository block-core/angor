using System.Linq;
using System.Windows.Input;
using AngorApp.Core;

namespace AngorApp.Sections.Browse.ProjectLookup;

public partial class ProjectLookupViewModelSample : ReactiveObject, IProjectLookupViewModel
{
    private bool hasResults;

    [Reactive] private SafeMaybe<IList<IProjectViewModel>> lookupResults;

    public bool HasResults
    {
        get => hasResults;
        set
        {
            var asMaybe = SampleData.GetProjects().Select(project => (IProjectViewModel)new ProjectViewModelSample(project)).ToList().AsSafeMaybe<IList<IProjectViewModel>>();
            LookupResults = hasResults ? asMaybe : new SafeMaybe<IList<IProjectViewModel>>(Maybe<IList<IProjectViewModel>>.None);
            hasResults = value;
        }
    }

    public IObservable<bool> IsBusy { get; set; } = Observable.Return(false);
    public ReactiveCommand<string, SafeMaybe<IList<IProjectViewModel>>> Lookup { get; }
    public ICommand GoToSelectedProject { get; }

    public string? ProjectId { get; set; }
    public IProjectViewModel SelectedProject { get; set; }

    public void Dispose()
    {
        Lookup.Dispose();
    }
}