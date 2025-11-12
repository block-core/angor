using Angor.Contexts.Funding.Projects.Application.Dtos;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.Statistics;

public class ProjectStatisticsViewModelSample : IProjectStatisticsViewModel
{
    public IAmountUI TotalInvested { get; set; } = new AmountUI(1000000); // Example total invested amount
    public IAmountUI AvailableBalance { get; set; } = new AmountUI(300000); // Example available balance
    public IAmountUI Withdrawable { get; set; } = new AmountUI(200000); // Example withdrawable amount
    public int TotalStages { get; set; } = 5; // Example total stages
    public NextStageDto? NextStage { get; set; } = new NextStageDto 
    { 
        StageIndex = 2, 
        ReleaseDate = DateTime.Now.AddDays(30), 
        DaysUntilRelease = 30, 
        PercentageToRelease = 25 
    };
    public int SpentTransactions { get; set; } = 2;
    public int AvailableTransactions { get; set; } = 3;
    public int TotalTransactions { get; set; } = 5;
}
