namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public interface IProjectStatisticsViewModel
{
    public IAmountUI TotalInvested { get; }
    public IAmountUI AvailableBalance { get; }
    
    public IAmountUI Withdrawable { get; }
    public int TotalStages { get; }
    public IStage? NextStage { get; }
    
    public int SpentTransactions { get; }
    public int AvailableTransactions { get; }
    public int TotalTransactions { get; }
}