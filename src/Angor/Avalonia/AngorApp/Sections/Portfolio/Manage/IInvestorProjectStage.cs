namespace AngorApp.Sections.Portfolio.Manage;

public interface IInvestorProjectStage
{
    int Stage { get; }
    IAmountUI Amount { get; }
    string Status { get; }
    bool IsSpent { get; }
}
