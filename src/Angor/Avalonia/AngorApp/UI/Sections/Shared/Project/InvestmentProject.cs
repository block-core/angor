using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;

namespace AngorApp.UI.Sections.Shared.Project;

public class InvestmentProject : Project, IInvestmentProject
{
    public InvestmentProject(ProjectDto dto, IProjectAppService projectAppService) : base(dto, projectAppService)
    {
    }

    protected override IProjectStats CreateStats(ProjectStatisticsDto statisticsDto)
    {
        var (fundingRaised, investorsCount, status) = CreateCommonStats(statisticsDto);

        return new InvestmentProjectStats(ProjectType, status, fundingRaised, investorsCount, Dto.Stages ?? [], statisticsDto.NextStage);
    }
}
