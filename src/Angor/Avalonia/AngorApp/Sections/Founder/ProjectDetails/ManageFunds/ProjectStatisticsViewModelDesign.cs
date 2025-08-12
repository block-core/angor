namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public class ProjectStatisticsViewModelDesign : IProjectStatisticsViewModel
{
    public IAmountUI TotalInvested { get; set; } = new AmountUI(1000000); // Example total invested amount
    public IAmountUI AvailableBalance { get; set; } = new AmountUI(300000); // Example available balance
    public IAmountUI Withdrawable { get; set; } = new AmountUI(200000); // Example withdrawable amount
    public int TotalStages { get; set; } = 5; // Example total stages
    public IStage? NextStage { get; set; }
    public int SpentTransactions { get; set; } = 2;
    public int AvailableTransactions { get; set; } = 3;
    public int TotalTransactions { get; set; } = 5;
}