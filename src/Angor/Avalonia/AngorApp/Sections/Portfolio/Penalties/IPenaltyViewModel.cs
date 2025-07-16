namespace AngorApp.Sections.Portfolio.Penalties;

public interface IPenaltyViewModel
{
    IAmountUI TotalAmount { get; }
    IAmountUI AmountInRecovery { get; }
    bool IsExpired { get; }
    string ProjectIdentifier { get; }
    int DaysLeftForPenalty { get; }
    string InvestorPubKey { get; }
}