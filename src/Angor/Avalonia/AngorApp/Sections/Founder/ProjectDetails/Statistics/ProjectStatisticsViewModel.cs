using System.Linq;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using AngorApp.Model.Projects;

namespace AngorApp.Sections.Founder.ProjectDetails.Statistics;

public class ProjectStatisticsViewModel(IFullProject fullProject) : IProjectStatisticsViewModel
{
    public IAmountUI TotalInvested { get; } = fullProject.TotalInvested;
    public IAmountUI AvailableBalance { get; } = fullProject.AvailableBalance;
    public IAmountUI Withdrawable { get; } = fullProject.WithdrawableAmount;
    public int TotalStages { get; } = fullProject.Stages.Count();
    public NextStageDto? NextStage { get; } = fullProject.NextStage;
    public int SpentTransactions { get; } = fullProject.SpentTransactions;
    public int AvailableTransactions { get; } = fullProject.AvailableTransactions;
    public int TotalTransactions { get; } = fullProject.TotalTransactions;
}
