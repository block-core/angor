using Angor.Contexts.Funding.Projects.Application.Dtos;

namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public class ProjectStatisticsViewModel : IProjectStatisticsViewModel
{
    public ProjectStatisticsViewModel(ProjectDto project)
    {
        TotalInvested = new AmountUI(project.Raised());
        TotalStages = project.Stages.Count;
        
        // TODO: We need to figure out how to get the rest of the data
    }

    public IAmountUI TotalInvested { get; }
    public int TotalStages { get; }
    public IAmountUI AvailableBalance { get; } = new AmountUI(12345);
    public IAmountUI Withdrawable { get; } = new AmountUI(12345);
    public IStage? NextStage { get; }
    public int SpentTransactions { get; } = 2;
    public int AvailableTransactions { get; } = 4;
    public int TotalTransactions { get; } = 5;
}