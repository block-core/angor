using Angor.Sdk.Funding.Projects.Application.Dtos;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.Statistics;

public interface IProjectStatisticsViewModel
{
    public IAmountUI TotalInvested { get; }
    public IAmountUI AvailableBalance { get; }
    
    public IAmountUI Withdrawable { get; }
    public int TotalStages { get; }
    public NextStageDto? NextStage { get; }
    
    public int SpentTransactions { get; }
    public int AvailableTransactions { get; }
    public int TotalTransactions { get; }
}
