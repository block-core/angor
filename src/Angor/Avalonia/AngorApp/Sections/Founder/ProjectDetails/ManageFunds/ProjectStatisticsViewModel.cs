using Angor.Contexts.Funding.Projects.Application.Dtos;

namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public class ProjectStatisticsViewModel : IProjectStatisticsViewModel
{
    public ProjectStatisticsViewModel(ProjectStatisticsDto statistics)
    {
        TotalInvested = new AmountUI(statistics.TotalInvested);
        AvailableBalance = new AmountUI(statistics.AvailableBalance);
        Withdrawable = new AmountUI(statistics.WithdrawableAmount);
        TotalStages = statistics.TotalStages;
        NextStage = statistics.NextStage;
        SpentTransactions = statistics.SpentTransactions;
        AvailableTransactions = statistics.AvailableTransactions;
        TotalTransactions = statistics.TotalTransactions;
    }

    public IAmountUI TotalInvested { get; }
    public IAmountUI AvailableBalance { get; }
    public IAmountUI Withdrawable { get; }
    public int TotalStages { get; }
    public NextStageDto? NextStage { get; }
    public int SpentTransactions { get; }
    public int AvailableTransactions { get; }
    public int TotalTransactions { get; }
}
