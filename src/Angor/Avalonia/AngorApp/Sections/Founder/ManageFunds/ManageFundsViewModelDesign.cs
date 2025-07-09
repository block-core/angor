using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ManageFunds;

public class ManageFundsViewModelDesign : IManageFundsViewModel
{
    public IEnhancedCommand Load { get; }
    public IProjectViewModel ProjectViewModel { get; set; }
    public IProjectStatisticsViewModel ProjectStatisticsViewModel { get; set; }
    public IStageClaimViewModel StageClaimViewModel { get; set; }
    public IAmountUI RaisedAmount { get; set; }
    public IAmountUI TargetAmount { get; set; }
}