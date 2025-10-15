using System.Reactive.Disposables;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.UI.Model.Implementation.Common;
using AngorApp.Core.Factories;
using AngorApp.Sections.Browse.ProjectLookup;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Sections.Browse;

public partial class BrowseSectionViewModel : ReactiveObject, IBrowseSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();
    [Reactive] private string? projectId;
    
    public BrowseSectionViewModel(
        IProjectAppService projectService,
        IProjectViewModelFactory projectViewModelFactory,
        IProjectLookupViewModelFactory projectLookupViewModelFactory,
        UIServices uiServices)
    {
        ProjectLookupViewModel = projectLookupViewModelFactory.Create().DisposeWith(disposable);

        var refresher = RefreshableCollection.Create(GetProjects(projectService, projectViewModelFactory), model => model.Project.Id)
            .DisposeWith(disposable);

        LoadProjects = refresher.Refresh;
        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Could not load projects");
        Projects = refresher.Items;
    }

    public ICollection<IProjectViewModel> Projects { get; }
    public IProjectLookupViewModel ProjectLookupViewModel { get; }
    public IEnhancedCommand<Result<IEnumerable<IProjectViewModel>>> LoadProjects { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }

    private static Func<Task<Result<IEnumerable<IProjectViewModel>>>> GetProjects(IProjectAppService projectService, IProjectViewModelFactory projectViewModelFactory)
    {
        return () => projectService.Latest()
            .MapEach(dto => dto.ToProject())
            .MapEach(projectViewModelFactory.Create);
    }
}
