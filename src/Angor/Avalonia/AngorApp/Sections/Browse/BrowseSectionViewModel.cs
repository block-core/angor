using System.Linq;
using System.Reactive.Disposables;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.Features.Invest;
using AngorApp.Sections.Browse.ProjectLookup;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Browse;

public partial class BrowseSectionViewModel : ReactiveObject, IBrowseSectionViewModel, IDisposable
{
    [Reactive] private string? projectId;
    private readonly CompositeDisposable disposable = new();

    [ObservableAsProperty] private IEnumerable<IProjectViewModel>? projects;

    public BrowseSectionViewModel(IProjectAppService projectService, INavigator navigator,
        InvestWizard investWizard,
        UIServices uiServices)
    {
        ProjectLookupViewModel = new ProjectLookupViewModel(projectService, navigator, investWizard, uiServices).DisposeWith(disposable);

        LoadProjects = ReactiveCommand.CreateFromObservable(() => Observable.FromAsync(projectService.Latest)
                .Map(list => list.Select(dto => dto.ToProject()))
                .Map(list => list.Select(IProjectViewModel (project) => new ProjectViewModel(project, projectService, navigator, uiServices, investWizard)).ToList()))
            .Enhance()
            .DisposeWith(disposable);

        IsBusy = this.WhenAnyValue(model => model.Projects)
            .WhereNotNull()
            .Select(models => MergeBusy(models.Select(model => model.GoToDetails.IsExecuting)))
            .Switch()
            .StartWith(false);
        
        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Could not load projects");

        projectsHelper = LoadProjects.Successes().ToProperty(this, x => x.Projects).DisposeWith(disposable);
        LoadProjects.Execute().Subscribe().DisposeWith(disposable);
    }

    public IObservable<bool> IsBusy { get; }
    public IProjectLookupViewModel ProjectLookupViewModel { get; }
    public IEnhancedCommand<Result<List<IProjectViewModel>>> LoadProjects { get; }
    
    // Merges a set of IObservable<bool> (true=start, false=end) into a single busy flag.
    private static IObservable<bool> MergeBusy(IEnumerable<IObservable<bool>> sources)
    {
        // Empty -> never busy
        var normalized = sources.Select(s => s.Select(b => b ? 1 : -1));

        return normalized
            .Merge()
            .Scan(0, (acc, delta) =>
            {
                var next = acc + delta;
                return next < 0 ? 0 : next; // clamp to zero
            })
            .Select(count => count > 0);
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
}