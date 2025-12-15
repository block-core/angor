using System.Reactive.Disposables;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Operations;
using AngorApp.UI.Sections.Browse.Details;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.FindProjects
{
    [Section("Find Projects", icon: "fa-magnifying-glass", sortIndex: 2)]
    [SectionGroup("INVESTOR")]
    public class FindProjectsSectionViewModel : IFindProjectsSectionViewModel, IDisposable
    {
        private readonly IProjectAppService projectAppService;
        private readonly Func<IFullProject, IProjectDetailsViewModel> detailsFactory;
        private readonly CompositeDisposable disposable = new();
        private readonly INavigator navigator;

        public FindProjectsSectionViewModel(IProjectAppService projectAppService, Func<IFullProject, IProjectDetailsViewModel> detailsFactory, INavigator navigator)
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

        public IEnhancedCommand<Result<IEnumerable<FindProjectItem>>> LoadProjects { get; set; }

        private Task<Result<IEnumerable<FindProjectItem>>> DoLoadItems()
        {
            // TODO: Suggested backend API change:
            // We need data from the Project Statistics (raised, number of investors), and ProjectDto does not have this.
            // Thus, we need to call GetProjectStatistics on EVERY item. This is very slow and might be changed.
            // Ideally, the ProjectDto could have everything we need.
            // This way, a single call will be enough and the performance will increase drastically. 
            return projectAppService
                .Latest(new LatestProjects.LatestProjectsRequest())
                .Map(response => response.Projects)
                .MapSequentially(dto => projectAppService.GetProjectStatistics(dto.Id)
                    .Map(statistics => new FindProjectItem(dto, statistics, projectAppService, detailsFactory, navigator)));
        }

        public IEnumerable<IFindProjectItem> Projects { get; }

        public void Dispose()
        {
            disposable.Dispose();
        }
    }
}