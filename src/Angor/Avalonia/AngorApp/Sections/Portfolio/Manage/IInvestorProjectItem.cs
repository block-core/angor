namespace AngorApp.Sections.Portfolio.Manage;

public interface IInvestorProjectItem
{
    public int Stage { get; }
    public IAmountUI Amount { get;  }
    public string Status { get; }
}