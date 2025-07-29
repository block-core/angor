using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public interface IManageFundsViewModel : IDisposable
{
    IEnhancedCommand Load { get; }
    IProjectViewModel ProjectViewModel { get; }
    IProjectStatisticsViewModel ProjectStatisticsViewModel { get; }
    IStageClaimViewModel StageClaimViewModel { get; }
    IUnfundedProjectViewModel UnfundedProjectViewModel { get;  }
    public IAmountUI RaisedAmount { get; }
    public IAmountUI TargetAmount { get; }
    public bool IsUnfunded { get; }
    public bool IsProjectStarted { get; }
}