using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Angor.UI.Model;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using Serilog;
using Zafiro.Avalonia.Controls.Navigation;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Sections.Browse.ProjectLookup;

public partial class ProjectLookupViewModel : ReactiveObject, IProjectLookupViewModel
{
    [ObservableAsProperty] private ICommand? goToSelectedProject;

    [ObservableAsProperty] private Maybe<IList<IProjectViewModel>> lookupResults;

    [Reactive] private string? projectId;
    [Reactive] private IProjectViewModel? selectedProject;

    public ProjectLookupViewModel(
        IProjectService projectService,
        IWalletProvider walletProvider,
        INavigator navigator,
        UIServices uiServices)
    {
        Lookup = ReactiveCommand.CreateFromTask<string, Maybe<IList<IProjectViewModel>>>(
            async pid =>
            {
                var maybeProject = await projectService.FindById(pid);
                Log.Debug("Got project {ProjectId}", pid);

                return maybeProject.Map<IProject, IList<IProjectViewModel>>(project =>
                {
                    var vm = new ProjectViewModel(walletProvider, project, navigator, uiServices);
                    return new List<IProjectViewModel> { vm };
                });
            }
        );

        lookupResultsHelper = Lookup.ToProperty(this, x => x.LookupResults);

        IsBusy = Lookup.IsExecuting;

        this.WhenAnyValue(x => x.ProjectId)
            .Where(pid => !string.IsNullOrWhiteSpace(pid))
            .Throttle(TimeSpan.FromSeconds(0.6), RxApp.MainThreadScheduler)
            .Do(pid => Log.Debug("Search for ProjectId {ProjectId}", pid))
            .InvokeCommand(Lookup!);

        this.WhenAnyValue(x => x.LookupResults).Values().Do(x => SelectedProject = x.FirstOrDefault()).Subscribe();

        goToSelectedProjectHelper = this.WhenAnyValue(x => x.SelectedProject!.GoToDetails).ToProperty(this, x => x.GoToSelectedProject);
    }

    public ReactiveCommand<string, Maybe<IList<IProjectViewModel>>> Lookup { get; }

    public IObservable<bool> IsBusy { get; }
}