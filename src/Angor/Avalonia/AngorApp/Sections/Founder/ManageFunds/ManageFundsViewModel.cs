using AngorApp.Sections.Founder.CreateProject.Stages;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ManageFunds;

public interface IManageFundsViewModel
{
    IEnhancedCommand Load { get; }
    IProjectViewModel ProjectViewModel { get; }
    IProjectStatisticsViewModel ProjectStatisticsViewModel { get; }
    IStageClaimViewModel StageClaimViewModel { get; }
    IUnfundedProjectViewModel UnfundedProjectViewModel { get;  }
    public IAmountUI RaisedAmount { get; }
    public IAmountUI TargetAmount { get; }
    public bool IsUnfunded { get; }
}

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

public class ProjectStatisticsViewModelDesign : IProjectStatisticsViewModel
{
    public IAmountUI TotalInvested { get; set; }
    public IAmountUI AvailableBalance { get; set; }
    public IAmountUI Withdrawable { get; set; }
    public int TotalStages { get; set; }
    public IStage? NextStage { get; set; }
    public int SpentTransactions { get; set; }
    public int AvailableTransactions { get; set; }
    public int TotalTransactions { get; set; }
}

public class ManageFundsViewModel : ReactiveObject, IManageFundsViewModel
{
    public ManageFundsViewModel()
    {
        ProjectViewModel = new ProjectViewModel()
        {
            Avatar = "https://images-assets.nasa.gov/image/PIA05062/PIA05062~thumb.jpg",
            Banner = "https://images-assets.nasa.gov/image/PIA05062/PIA05062~thumb.jpg",
            Name = "Sample Project",
            ShortDescription = "This is a sample project description that is meant to be short and concise",
        };
    }
    
    public IEnhancedCommand Load { get; }

    public IProjectViewModel ProjectViewModel { get; }
    public IProjectStatisticsViewModel ProjectStatisticsViewModel { get; }
    public IStageClaimViewModel StageClaimViewModel { get; }
    public IUnfundedProjectViewModel UnfundedProjectViewModel { get; }
    public IAmountUI RaisedAmount { get; }
    public IAmountUI TargetAmount { get; }
    public bool IsUnfunded { get; }
}