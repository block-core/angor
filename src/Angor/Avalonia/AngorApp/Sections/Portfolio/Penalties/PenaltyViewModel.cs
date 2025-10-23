using Angor.Contexts.Funding.Investor.Dtos;

namespace AngorApp.Sections.Portfolio.Penalties;

public class PenaltyViewModel : IPenaltyViewModel
{
    public PenaltyViewModel(PenaltiesDto dto)
    {
        InvestorPubKey = dto.InvestorPubKey;
        DaysLeftForPenalty = dto.DaysLeftForPenalty;
        ProjectName = dto.ProjectName;
        IsExpired = dto.IsExpired;
        TotalAmount = new AmountUI(dto.TotalAmountSats);
        AmountInRecovery = new AmountUI(dto.AmountInRecovery);
    }
    
    public IAmountUI TotalAmount { get; }
    public IAmountUI AmountInRecovery { get; }
    public bool IsExpired { get; }
    public string ProjectName { get; }
    public int DaysLeftForPenalty { get; }
    public string InvestorPubKey { get; }
}