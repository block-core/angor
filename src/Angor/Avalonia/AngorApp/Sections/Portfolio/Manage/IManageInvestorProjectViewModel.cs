namespace AngorApp.Sections.Portfolio.Manage;

public interface IManageInvestorProjectViewModel
{
    IEnhancedCommand ViewTransaction { get; }
    IObservable<IEnhancedCommand> Action { get; }
    IEnhancedCommand Load { get; }
    IObservable<RecoveryState> State { get; }
}
