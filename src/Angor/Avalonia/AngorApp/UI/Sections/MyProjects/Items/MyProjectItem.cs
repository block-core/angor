using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using AngorApp.Core.Factories;
using AngorApp.Model.ProjectsV2;
using AngorApp.UI.Sections.MyProjects.ManageFunds;
using Zafiro.UI.Navigation;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.MyProjects.Items;

public class MyProjectItem : IMyProjectItem, IDisposable
{
    public MyProjectItem(
        ProjectDto dto,
        IProjectAppService projectAppService,
        IProjectInvestCommandFactory projectInvestCommandFactory,
        Func<ProjectId, IManageFundsViewModel> detailsFactory,
        INavigator navigator)
    {
        Project = Model.ProjectsV2.Project.Create(dto, projectAppService, projectInvestCommandFactory.Create(dto.Id, dto.FundingStartDate, dto.FundingEndDate, dto.ProjectType));
        ManageFunds = EnhancedCommand.Create(() => navigator.Go(() => detailsFactory(Project.Id)));
    }

    public IProject Project { get; }
    public IEnhancedCommand ManageFunds { get; }

    public void Dispose()
    {
        (Project as IDisposable)?.Dispose();
    }
}
