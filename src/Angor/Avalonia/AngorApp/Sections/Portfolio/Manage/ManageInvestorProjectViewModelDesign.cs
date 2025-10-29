using Angor.Contexts.Funding.Investor.Dtos;

namespace AngorApp.Sections.Portfolio.Manage;

public class ManageInvestorProjectViewModelDesign : IManageInvestorProjectViewModel
{
    public IEnhancedCommand ViewTransaction { get; } = ReactiveCommand.Create(() => { }).Enhance();
    public IObservable<IEnhancedCommand> BatchAction { get; }
    public IEnhancedCommand<Result<RecoveryStateViewModel>> Load { get; set; } 
    public IObservable<RecoveryStateViewModel> State { get; }
}
