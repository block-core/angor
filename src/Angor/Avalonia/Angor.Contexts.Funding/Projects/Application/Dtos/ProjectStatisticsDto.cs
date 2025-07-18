namespace Angor.Contexts.Funding.Projects.Application.Dtos;

public class ProjectStatisticsDto
{
    public int TotalStages  { get; set; }

    public StageDto NextStage  {get; set;} 
    public TimeSpan? TimeUntilNextStage { get; set; }
    public long   TotalAvailableInvestedAmount  { get; set; }
    public int TotalInvestedTransactions  { get; set; }
    public long TotalSpentAmount  { get; set; }
    public int TotalSpentTransactions  { get; set; }
    public long CurrentWithdrawableAmount  { get; set; }
    public long TotalInvestedAmount { get; set; }
}