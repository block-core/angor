using Angor.Contexts.Funding.Investor;
using AngorApp.Sections.Founder.ManageFunds.Models.Design;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ManageFunds.Models;

public class ManageFundsViewModel : ReactiveObject, IManageFundsViewModel
{
    public ManageFundsViewModel(IInvestmentAppService appService)
    {
       // TODO: We need to fetch project information from the app service using the Load command.
       Load = ReactiveCommand.Create(() => { }).Enhance();
       ProjectViewModel = new ProjectViewModelDesign();
       ProjectStatisticsViewModel = new ProjectStatisticsViewModelDesign();
       UnfundedProjectViewModel = new UnfundedProjectViewModelDesign();
       StageClaimViewModel = new StageClaimViewModelDesign();
       TargetAmount = new AmountUI(1000000); // Example target amount
       RaisedAmount = new AmountUI(500000); // Example raised amount
    }
    
    public IEnhancedCommand Load { get; }

    public IProjectViewModel ProjectViewModel { get; }
    public IProjectStatisticsViewModel ProjectStatisticsViewModel { get; }
    public IStageClaimViewModel StageClaimViewModel { get; }
    public IUnfundedProjectViewModel UnfundedProjectViewModel { get; }
    public IAmountUI RaisedAmount { get; }
    public IAmountUI TargetAmount { get; }
    public bool IsUnfunded { get; }
}