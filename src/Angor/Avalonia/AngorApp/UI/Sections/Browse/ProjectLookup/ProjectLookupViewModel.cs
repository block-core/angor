using System.Linq;
using System.Windows.Input;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using AngorApp.Core;
using AngorApp.Core.Factories;
using Serilog;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Sections.Browse.ProjectLookup;

public partial class ProjectLookupViewModel : ReactiveObject, IProjectLookupViewModel
{
    [ObservableAsProperty] private ICommand? goToSelectedProject;

    [ObservableAsProperty] private SafeMaybe<IList<IProjectViewModel>> lookupResults;

    [Reactive] private string? projectId;
    [Reactive] private IProjectViewModel? selectedProject;

    public ProjectLookupViewModel(IProjectAppService projectAppService, IProjectViewModelFactory projectViewModelFactory, UIServices uiServices)
    {
        lookupResults = new SafeMaybe<IList<IProjectViewModel>>(Maybe<IList<IProjectViewModel>>.None);

        Lookup = ReactiveCommand.CreateFromTask<string, Result<SafeMaybe<IList<IProjectViewModel>>>>(async pid =>
            {
                Result<SafeMaybe<IList<IProjectViewModel>>> project = await projectAppService.TryGet(new ProjectId(pid))
                    .Map(maybeProject => maybeProject.Map(dto => dto.ToProject())
                        .Tap(p => Log.Debug("Got project {ProjectId}", p))
                        .Map<IProject, IList<IProjectViewModel>>(project =>
                        {
                            var vm = projectViewModelFactory.Create(project);
                            return new List<IProjectViewModel> { vm };
                        }).AsSafeMaybe());

                return project;
            }
        );
        
        Lookup.HandleErrorsWith(uiServices.NotificationService, "Could not lookup project");

        lookupResultsHelper = Lookup.Successes().ToProperty(this, x => x.LookupResults);

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

    public ReactiveCommand<string, Result<SafeMaybe<IList<IProjectViewModel>>>> Lookup { get; }

    public IObservable<bool> IsBusy { get; }

    public void Dispose()
    {
        goToSelectedProjectHelper.Dispose();
        lookupResultsHelper.Dispose();
        Lookup.Dispose();
    }
}