using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ManageFunds.Models;

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