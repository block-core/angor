using Angor.Contexts.Funding.Investor;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ManageFunds;

public interface IManageFundsViewModel
{
    IEnhancedCommand Load { get; }
    IProjectViewModel ProjectViewModel { get; }
    IProjectStatisticsViewModel ProjectStatisticsViewModel { get; }
    IStageClaimViewModel StageClaimViewModel { get; }
    IUnfundedProjectViewModel UnfundedProjectViewModel { get;  }
    public IAmountUI RaisedAmount { get; }
    public IAmountUI TargetAmount { get; }
    public bool IsUnfunded { get; }
}

public interface IProjectStatisticsViewModel
{
    public IAmountUI TotalInvested { get; }
    public IAmountUI AvailableBalance { get; }
    
    public IAmountUI Withdrawable { get; }
    public int TotalStages { get; }
    public IStage? NextStage { get; }
    
    public int SpentTransactions { get; }
    public int AvailableTransactions { get; }
    public int TotalTransactions { get; }
}

public class ProjectStatisticsViewModelDesign : IProjectStatisticsViewModel
{
    public IAmountUI TotalInvested { get; set; } = new AmountUI(1000000); // Example total invested amount
    public IAmountUI AvailableBalance { get; set; } = new AmountUI(300000); // Example available balance
    public IAmountUI Withdrawable { get; set; } = new AmountUI(200000); // Example withdrawable amount
    public int TotalStages { get; set; } = 5; // Example total stages
    public IStage? NextStage { get; set; }
    public int SpentTransactions { get; set; } = 2;
    public int AvailableTransactions { get; set; } = 3;
    public int TotalTransactions { get; set; } = 5;
}

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