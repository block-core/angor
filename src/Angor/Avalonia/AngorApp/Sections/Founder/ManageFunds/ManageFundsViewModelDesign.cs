using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ManageFunds;

public class ManageFundsViewModelDesign : ReactiveObject, IManageFundsViewModel
{
    public IEnhancedCommand Load { get; }
    public IProjectViewModel ProjectViewModel { get; set; } = new ProjectViewModelDesign();
    public IProjectStatisticsViewModel ProjectStatisticsViewModel { get; set; } = new ProjectStatisticsViewModelDesign();
    public IStageClaimViewModel StageClaimViewModel { get; set; } = new StageClaimViewModelDesign()
    {
    };
    public IUnfundedProjectViewModel UnfundedProjectViewModel { get; set; } = new UnfundedProjectViewModelDesign();
    public IAmountUI RaisedAmount { get; set; } = new AmountUI(500000); // Example raised amount
    public IAmountUI TargetAmount { get; set; } = new AmountUI(1000000); // Example target amount
    public bool IsUnfunded { get; set; }
}