namespace Angor.Contexts.Funding.Founder.Dtos;

public record InvestorStatsDto
{
    public required int FundedProjects { get; init; }
    public required long TotalInvested { get; init; }
    public required long RecoveredToPenalty { get; init; }
    public required int ProjectsInRecovery { get; init; }
}