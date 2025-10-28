using Angor.Contexts.Funding.Investor.Dtos;

namespace AngorApp.Sections.Portfolio.Manage;

public class ManageInvestorProjectViewModelDesign : IManageInvestorProjectViewModel
{
    public IEnhancedCommand ViewTransaction { get; } = ReactiveCommand.Create(() => { }).Enhance();
    public IObservable<IEnhancedCommand> BatchAction { get; }
    public IEnhancedCommand<Result<RecoveryState>> Load { get; set; } 
    public IObservable<RecoveryState> State { get; }
}
