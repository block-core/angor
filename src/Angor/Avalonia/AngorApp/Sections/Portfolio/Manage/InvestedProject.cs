using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Shared;

namespace AngorApp.Sections.Portfolio.Manage;

public class InvestedProject : IInvestedProject
{
    public InvestedProject(InvestorProjectRecoveryDto dto)
    {
        TotalFunds = new AmountUI(dto.TotalSpendable);
        ExpiryDate = dto.ExpiryDate;
        PenaltyPeriod = TimeSpan.FromDays(dto.PenaltyDays);
        Name = dto.Name ?? dto.ProjectIdentifier;
        ProjectId = new ProjectId(dto.ProjectIdentifier);
    }

    public ProjectId ProjectId { get; }
    
    public IAmountUI TotalFunds { get; }
    public DateTime ExpiryDate { get; }
    public TimeSpan PenaltyPeriod { get; }
    public string Name { get; }
}