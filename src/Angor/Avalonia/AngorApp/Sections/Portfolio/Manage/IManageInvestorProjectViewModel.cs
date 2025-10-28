namespace AngorApp.Sections.Portfolio.Manage;

public interface IManageInvestorProjectViewModel
{
    IEnhancedCommand ViewTransaction { get; }
    IObservable<IEnhancedCommand> BatchAction { get; }
    IEnhancedCommand<Result<RecoveryState>> Load { get; }
    IObservable<RecoveryState> State { get; }
}
