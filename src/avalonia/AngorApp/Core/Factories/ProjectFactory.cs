using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Shared.Models;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment;
using Zafiro.UI.Navigation;
using FundManageFunds = AngorApp.UI.Sections.MyProjects.ManageFunds.Fund;

namespace AngorApp.Core.Factories;

public class ProjectFactory(
    IProjectAppService projectAppService,
    IProjectInvestCommandFactory projectInvestCommandFactory,
    Func<IInvestmentProject, IManageFundsViewModel> manageFundsFactory,
    Func<IFundProject, FundManageFunds.IManageFundsViewModel> fundManageFundsFactory,
    INavigator navigator
)
    : IProjectFactory
{
    public IProject Create(ProjectDto dto)
    {
        var invest = projectInvestCommandFactory.Create(dto.Id, dto.FundingStartDate, dto.FundingEndDate, dto.ProjectType);
        return dto.ProjectType switch
        {
            ProjectType.Invest => CreateInvestmentProject(dto, invest),
            ProjectType.Fund => CreateFundProject(dto, invest),
            _ => throw new ArgumentOutOfRangeException(nameof(dto.ProjectType), "Unsupported project type")
        };
    }

    private IProject CreateInvestmentProject(ProjectDto dto, IEnhancedCommand<Result> invest)
    {
        IInvestmentProject? project = null;
        var manageFunds = EnhancedCommand.Create(() =>
        {
            if (project is not null)
            {
                navigator.Go(() => manageFundsFactory(project));
            }
        }, text: "Manage Funds");

        project = new InvestmentProject(dto, projectAppService, invest, manageFunds);
        return project;
    }

    private IProject CreateFundProject(ProjectDto dto, IEnhancedCommand<Result> invest)
    {
        IFundProject? project = null;
        var manageFunds = EnhancedCommand.Create(() =>
        {
            if (project is not null)
            {
                navigator.Go(() => fundManageFundsFactory(project));
            }
        }, text: "Manage Funds");

        project = new FundProject(dto, projectAppService, invest, manageFunds);
        return project;
    }
}
