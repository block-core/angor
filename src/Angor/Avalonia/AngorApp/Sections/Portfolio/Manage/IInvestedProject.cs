namespace AngorApp.Sections.Portfolio.Manage;

public interface IInvestedProject
{
    public IAmountUI TotalFunds { get; }
    public DateTime ExpiryDate { get; }
    public TimeSpan PenaltyPeriod { get; }
    public string Name { get; }
}