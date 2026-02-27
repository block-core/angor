using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;

namespace AngorApp.UI.Sections.Shared.Project;

public class SubscriptionProject : Project, ISubscriptionProject
{
    public SubscriptionProject(ProjectDto dto, IProjectAppService projectAppService) : base(dto, projectAppService)
    {
    }

    protected override IProjectStats CreateStats(ProjectStatisticsDto statisticsDto)
    {
        var (fundingRaised, investorsCount, status) = CreateCommonStats(statisticsDto);

        return new SubscriptionProjectStats(ProjectType, status, fundingRaised, investorsCount, Dto.DynamicStagePatterns ?? [], statisticsDto.DynamicStages ?? []);
    }
}
