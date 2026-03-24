namespace Angor.Sdk.Funding.Projects.Dtos;

public class ProjectStatisticsDto
{
    public long TotalInvested { get; set; }
    public long AvailableBalance { get; set; }
    public long WithdrawableAmount { get; set; }
    public int TotalStages { get; set; }
    public NextStageDto? NextStage { get; set; }
    public int TotalTransactions { get; set; }
    public int SpentTransactions { get; set; }
    public int AvailableTransactions => TotalTransactions - SpentTransactions;
    public long SpentAmount { get; set; }
    public int? TotalInvestors { get; set; }
    public List<DynamicStageDto>? DynamicStages { get; set; }
}