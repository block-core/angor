using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.Contexts.Funding.Investor.Dtos;

public class CreateProjectDto
{
    public IEnumerable<CreateProjectStageDto> Stages { get; init; }
    public Amount TargetAmount { get; init; }
    public int PenaltyDays { get; set; }
}

public record CreateProjectStageDto(DateOnly Deadline, double RatioOverTotal);