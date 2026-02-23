using Angor.Sdk.Funding.Investor.Dtos;

namespace AngorApp.UI.Sections.Portfolio.Manage;

public class ManageInvestorProjectViewModelSample : IManageInvestorProjectViewModel
{
    public IEnhancedCommand ViewTransaction { get; } = ReactiveCommand.Create(() => { }).Enhance();
    public IObservable<IEnhancedCommand> BatchAction { get; } = null!;
    public IEnhancedCommand<Result<RecoveryStateViewModel>> Load { get; set; } = null!;
    public IObservable<RecoveryStateViewModel> State { get; } = null!;
}
