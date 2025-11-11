namespace AngorApp.Sections.Portfolio.Manage;

public class InvestorProjectStageSample : IInvestorProjectStage
{
    public int Stage { get; set; } = 1;
    public IAmountUI Amount { get; set; } = new AmountUI(1234);
    public string Status { get; set; } = "In Progress";
    public bool IsSpent { get; set; }
}
