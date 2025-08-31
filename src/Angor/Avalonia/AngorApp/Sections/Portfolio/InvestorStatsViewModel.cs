using Angor.Contexts.Funding.Founder.Dtos;

namespace AngorApp.Sections.Portfolio;

public class InvestorStatsViewModel(InvestorStatsDto dto)
{
    public IAmountUI TotalInvested { get; } = new AmountUI(dto.TotalInvested);
    public IAmountUI RecoveredToPenalty { get; }  = new AmountUI(dto.TotalInvested);
    public int ProjectsInRecovery { get; } = dto.ProjectsInRecovery;
    public int FundedProjects { get; } = dto.FundedProjects;
}