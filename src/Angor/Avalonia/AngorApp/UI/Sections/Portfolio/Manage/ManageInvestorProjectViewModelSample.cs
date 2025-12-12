using Angor.Sdk.Funding.Investor.Dtos;

namespace AngorApp.UI.Sections.Portfolio.Manage;

public class ManageInvestorProjectViewModelSample : IManageInvestorProjectViewModel
{
    public IEnhancedCommand ViewTransaction { get; } = ReactiveCommand.Create(() => { }).Enhance();
    public IObservable<IEnhancedCommand> BatchAction { get; }
    public IEnhancedCommand<Result<RecoveryStateViewModel>> Load { get; set; } 
    public IObservable<RecoveryStateViewModel> State { get; }
}
