using Angor.Contexts.Funding.Investor.Dtos;

namespace AngorApp.Sections.Portfolio.Manage;

public class ManageInvestorProjectViewModelDesign : IManageInvestorProjectViewModel
{
    public IEnhancedCommand ViewTransaction { get; } = ReactiveCommand.Create(() => { }).Enhance();
    public IObservable<IEnhancedCommand> BatchAction { get; }
    public IEnhancedCommand Load { get; } = ReactiveCommand.Create(() => Result.Success(new InvestorProjectRecoveryDto())).Enhance();
    public IObservable<RecoveryState> State { get; }
}
