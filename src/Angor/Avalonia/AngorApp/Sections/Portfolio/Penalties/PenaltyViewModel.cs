using Angor.Contexts.Funding.Investor.Operations;

namespace AngorApp.Sections.Portfolio.Penalties;

public class PenaltyViewModel : IPenaltyViewModel
{
    public PenaltyViewModel(PenaltiesDto dto)
    {
        InvestorPubKey = dto.InvestorPubKey;
        DaysLeftForPenalty = dto.DaysLeftForPenalty;
        ProjectIdentifier = dto.ProjectIdentifier;
        IsExpired = dto.IsExpired;
        TotalAmount = new AmountUI(dto.TotalAmountSats);
        AmountInRecovery = new AmountUI(dto.AmountInRecovery);
    }
    
    public IAmountUI TotalAmount { get; }
    public IAmountUI AmountInRecovery { get; }
    public bool IsExpired { get; }
    public string ProjectIdentifier { get; }
    public int DaysLeftForPenalty { get; }
    public string InvestorPubKey { get; }
}