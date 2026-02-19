using System.Reactive.Disposables;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Operations;
using AngorApp.UI.Sections.FindProjects.Details;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.FindProjects;

[Section("Find Projects", icon: "fa-magnifying-glass", sortIndex: 2)]
[SectionGroup("INVESTOR")]
public class FindProjectsSectionViewModel : IFindProjectsSectionViewModel, IDisposable
{
    private readonly IProjectAppService projectAppService;
    private readonly Func<IFullProject, IDetailsViewModel> detailsFactory;
    private readonly CompositeDisposable disposable = new();
    private readonly INavigator navigator;

    public FindProjectsSectionViewModel(IProjectAppService projectAppService, Func<IFullProject, IDetailsViewModel> detailsFactory, INavigator navigator)
    {
        this.projectAppService = projectAppService;
        this.detailsFactory = detailsFactory;
        this.navigator = navigator;
        LoadProjects = EnhancedCommand.Create(DoLoadItems).DisposeWith(disposable);

        LoadProjects.Successes()
                    .EditDiff(item => item.Id)
                    .Bind(out var projects)
                    .Subscribe()
                    .DisposeWith(disposable);

        Projects = projects;
    }

    public IEnhancedCommand<Result<IEnumerable<FindProjectItem>>> LoadProjects { get; }

    private Task<Result<IEnumerable<FindProjectItem>>> DoLoadItems()
    {
        return projectAppService
               .Latest(new LatestProjects.LatestProjectsRequest())
               .Map(response => response.Projects.Select(dto =>
                                                             new FindProjectItem(dto, projectAppService, detailsFactory, navigator)));
    }

    public IEnumerable<IFindProjectItem> Projects { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}