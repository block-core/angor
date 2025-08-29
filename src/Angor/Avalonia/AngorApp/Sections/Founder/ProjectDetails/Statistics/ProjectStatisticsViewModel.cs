using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.UI.Model.Implementation.Projects;

namespace AngorApp.Sections.Founder.ProjectDetails.Statistics;

public class ProjectStatisticsViewModel(FullProject fullProject) : IProjectStatisticsViewModel
{
    public IAmountUI TotalInvested { get; } = new AmountUI(fullProject.Stats.TotalInvested);
    public IAmountUI AvailableBalance { get; } = new AmountUI(fullProject.Stats.AvailableBalance);
    public IAmountUI Withdrawable { get; } = new AmountUI(fullProject.Stats.WithdrawableAmount);
    public int TotalStages { get; } = fullProject.Stats.TotalStages;
    public NextStageDto? NextStage { get; } = fullProject.Stats.NextStage;
    public int SpentTransactions { get; } = fullProject.Stats.SpentTransactions;
    public int AvailableTransactions { get; } = fullProject.Stats.AvailableTransactions;
    public int TotalTransactions { get; } = fullProject.Stats.TotalTransactions;
}
