namespace AngorApp.UI.Sections.Portfolio.Manage;

public interface IManageInvestorProjectViewModel
{
    IEnhancedCommand<Result<RecoveryStateViewModel>> Load { get; }
    IObservable<RecoveryStateViewModel> State { get; }
}
