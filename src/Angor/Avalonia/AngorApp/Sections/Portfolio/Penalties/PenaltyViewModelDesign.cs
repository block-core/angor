namespace AngorApp.Sections.Portfolio.Penalties;

public class PenaltyViewModelDesign : IPenaltyViewModel
{
    public IAmountUI TotalAmount { get; } = new AmountUI(123456789);
    public bool IsExpired { get; } = false;
    public IAmountUI AmountInRecovery { get; } = new AmountUI(244124);
    public string ProjectIdentifier { get; } = "test-project-identifier";
    public int DaysLeftForPenalty { get; } = 98;
    public string InvestorPubKey { get; } = "test-investor-pubkey";
}