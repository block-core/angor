namespace Angor.Contexts.Funding.Projects.Application.Dtos;

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
}

public class NextStageDto
{
    public int StageIndex { get; set; }
    public DateTime ReleaseDate { get; set; }
    public int DaysUntilRelease { get; set; }
    public decimal PercentageToRelease { get; set; }
}