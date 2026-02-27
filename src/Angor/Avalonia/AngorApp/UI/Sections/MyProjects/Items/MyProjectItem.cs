using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using AngorApp.UI.Sections.MyProjects.ManageFunds;
using AngorApp.UI.Sections.Shared;
using AngorApp.UI.Sections.Shared.Project;
using SharedProjectFactory = AngorApp.UI.Sections.Shared.Project.Project;
using Zafiro.UI.Navigation;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.MyProjects.Items;

public class MyProjectItem : IMyProjectItem, IDisposable
{
    public MyProjectItem(
        ProjectDto dto,
        IProjectAppService projectAppService,
        Func<ProjectId, IManageFundsViewModel> detailsFactory,
        INavigator navigator)
    {
        Project = SharedProjectFactory.Create(dto, projectAppService);
        ManageFunds = EnhancedCommand.Create(() => navigator.Go(() => detailsFactory(Project.Id)));
    }

    public IProject Project { get; }
    public IEnhancedCommand ManageFunds { get; }

    public void Dispose()
    {
        (Project as IDisposable)?.Dispose();
    }
}
