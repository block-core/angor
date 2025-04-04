using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.UI.Model;
using Angor.Contexts.Wallet.Application;
using AngorApp.Core;
using AngorApp.UI.Services;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using Serilog;
using Zafiro.Avalonia.Controls.Navigation;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Sections.Browse.ProjectLookup;

public partial class ProjectLookupViewModel : ReactiveObject, IProjectLookupViewModel
{
    [ObservableAsProperty] private ICommand? goToSelectedProject;

    [ObservableAsProperty] private SafeMaybe<IList<IProjectViewModel>> lookupResults;

    [Reactive] private string? projectId;
    [Reactive] private IProjectViewModel? selectedProject;

    public ProjectLookupViewModel(
        IProjectAppService projectService,
        IWalletAppService walletAppService,
        INavigator navigator,
        UIServices uiServices)
    {
        lookupResults = new SafeMaybe<IList<IProjectViewModel>>(Maybe<IList<IProjectViewModel>>.None);

        Lookup = ReactiveCommand.CreateFromTask<string, SafeMaybe<IList<IProjectViewModel>>>(
            async pid =>
            {
                var maybeProject = await projectService.FindById(new ProjectId(pid)).Map(dto => dto.ToProject());
                Log.Debug("Got project {ProjectId}", pid);

                return maybeProject.Map<IProject, IList<IProjectViewModel>>(project =>
                {
                    var vm = new ProjectViewModel(walletAppService, project, navigator, uiServices);
                    return new List<IProjectViewModel> { vm };
                }).AsSafeMaybe();
            }
        );

        lookupResultsHelper = Lookup.ToProperty(this, x => x.LookupResults);

        IsBusy = Lookup.IsExecuting;

        this.WhenAnyValue(x => x.ProjectId)
            .Where(pid => !string.IsNullOrWhiteSpace(pid))
            .Throttle(TimeSpan.FromSeconds(0.6), RxApp.MainThreadScheduler)
            .Do(pid => Log.Debug("Search for ProjectId {ProjectId}", pid))
            .InvokeCommand(Lookup!);

        this.WhenAnyValue(x => x.LookupResults).Select(x => x.Maybe).Values()
            .Do(x => SelectedProject = x.FirstOrDefault()).Subscribe();

        goToSelectedProjectHelper = this.WhenAnyValue(x => x.SelectedProject!.GoToDetails)
            .ToProperty(this, x => x.GoToSelectedProject);
    }

    public ReactiveCommand<string, SafeMaybe<IList<IProjectViewModel>>> Lookup { get; }

    public IObservable<bool> IsBusy { get; }
}