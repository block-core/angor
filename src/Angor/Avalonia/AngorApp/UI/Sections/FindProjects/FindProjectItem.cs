using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using AngorApp.Model.ProjectsV2;
using AngorApp.UI.Sections.FindProjects.Details;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.FindProjects
{
    public class FindProjectItem : IFindProjectItem, IDisposable
    {
        public FindProjectItem(
            ProjectDto dto,
            IProjectAppService projectAppService,
            Func<IProject, IDetailsViewModel> detailsFactory,
            INavigator navigator)
        {
            Project = Model.ProjectsV2.Project.Create(dto, projectAppService);
            GoToDetails = EnhancedCommand.Create(() => navigator.Go(() => detailsFactory(Project)));
        }

        public IProject Project { get; }
        public IEnhancedCommand GoToDetails { get; }

        public void Dispose()
        {
            (Project as IDisposable)?.Dispose();
        }
    }
}
