namespace AngorApp.Sections.Portfolio.Manage;

public class InvestorProjectStage : ReactiveObject, IInvestorProjectStage
{
    public InvestorProjectStage(int stage, IAmountUI amount, bool isSpent, string status)
    {
        Stage = stage;
        Amount = amount;
        Status = status;
        IsSpent = isSpent;
    }

    public int Stage { get; }
    public IAmountUI Amount { get; }
    public string Status { get; }
    public bool IsSpent { get; }
}