using System.Linq;
using System.Reactive.Disposables;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.Features.Invest;
using AngorApp.Sections.Browse.ProjectLookup;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Mixins;
using Zafiro.Reactive;
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
            .Map(list => list.Select(IProjectViewModel (project) => new ProjectViewModel(project, navigator, uiServices, investWizard))))
            .Enhance()
            .DisposeWith(disposable);

        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Could not load projects");

        projectsHelper = LoadProjects.Successes().ToProperty(this, x => x.Projects).DisposeWith(disposable);
        LoadProjects.Execute().Subscribe().DisposeWith(disposable);
    }


    public IProjectLookupViewModel ProjectLookupViewModel { get; }

    public IEnhancedCommand<Result<IEnumerable<IProjectViewModel>>> LoadProjects { get; }


    public void Dispose()
    {
        disposable.Dispose();
    }
}