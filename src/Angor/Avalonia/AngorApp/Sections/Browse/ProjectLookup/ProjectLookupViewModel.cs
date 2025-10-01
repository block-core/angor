using System.Linq;
using System.Windows.Input;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.Core;
using AngorApp.Features.Invest;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Serilog;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Browse.ProjectLookup;

public partial class ProjectLookupViewModel : ReactiveObject, IProjectLookupViewModel ,IDisposable
{
    [ObservableAsProperty] private ICommand? goToSelectedProject;

    [ObservableAsProperty] private SafeMaybe<IList<IProjectViewModel>> lookupResults;

    [Reactive] private string? projectId;
    [Reactive] private IProjectViewModel? selectedProject;

    public ProjectLookupViewModel(IProjectAppService projectAppService,
        INavigator navigator,
        InvestWizard investWizard,
        UIServices uiServices,
        IInvestmentAppService investmentAppService)
    {
        lookupResults = new SafeMaybe<IList<IProjectViewModel>>(Maybe<IList<IProjectViewModel>>.None);

        Lookup = ReactiveCommand.CreateFromTask<string, SafeMaybe<IList<IProjectViewModel>>>(
            async pid =>
            {
                var maybeProject = await projectAppService.FindById(new ProjectId(pid)).Map(dto => dto.ToProject());
                Log.Debug("Got project {ProjectId}", pid);

                return maybeProject.Map<IProject, IList<IProjectViewModel>>(project =>
                {
                    var vm = new ProjectViewModel(project, projectAppService, navigator, uiServices, investWizard, investmentAppService);
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

    public void Dispose()
    {
        goToSelectedProjectHelper.Dispose();
        lookupResultsHelper.Dispose();
        Lookup.Dispose();
    }
}