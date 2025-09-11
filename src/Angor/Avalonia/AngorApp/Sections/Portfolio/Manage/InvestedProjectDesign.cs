namespace AngorApp.Sections.Portfolio.Manage;

public class InvestedProjectDesign : IInvestedProject
{
    public IAmountUI TotalFunds { get; } = new AmountUI(12345);
    public DateTime ExpiryDate { get; } = DateTime.Today;
    public TimeSpan PenaltyPeriod { get; } = TimeSpan.FromDays(2);
    public string Name { get; } = "Test Project";
}