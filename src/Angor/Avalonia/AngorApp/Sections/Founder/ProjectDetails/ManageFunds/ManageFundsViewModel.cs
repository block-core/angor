using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.Extensions;
using AngorApp.UI.Services;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public partial class ManageFundsViewModel : ReactiveObject, IManageFundsViewModel
{
    private readonly ObservableAsPropertyHelper<IProjectStatisticsViewModel> projectStatisticsViewModelHelper;
    
    public ManageFundsViewModel(ProjectDto project, IProjectAppService projectAppService, IWalletRoot walletRoot)
    {
       var defaultStatistics = new ProjectStatisticsDto
       {
           TotalInvested = project.Raised(),
           AvailableBalance = 0,
           WithdrawableAmount = 0,
           TotalStages = project.Stages.Count,
           NextStage = null,
           TotalTransactions = 0,
           SpentTransactions = 0,
           AvailableTransactions = 0,
           SpentAmount = 0
       };
       
       var loadCommand = WalletCommand.Create(
           async wallet => 
           {
               var statisticsResult = await projectAppService.GetProjectStatistics(wallet.Id.Value, project.Id);
               
               if (statisticsResult.IsSuccess)
               {
                   return Result.Success<IProjectStatisticsViewModel>(new ProjectStatisticsViewModel(statisticsResult.Value));
               }
               
               // Return default statistics if fetch fails
               return Result.Success<IProjectStatisticsViewModel>(new ProjectStatisticsViewModel(defaultStatistics));
           },
           walletRoot);
       
       Load = loadCommand.Enhance();
       
       // Create property from the command results
       projectStatisticsViewModelHelper = loadCommand
           .Select(result => result.IsSuccess ? result.Value : new ProjectStatisticsViewModel(defaultStatistics))
           .StartWith(new ProjectStatisticsViewModel(defaultStatistics))
           .ToProperty(this, x => x.ProjectStatisticsViewModel);
       
       ProjectViewModel = new ProjectViewModelDesign();
       UnfundedProjectViewModel = new UnfundedProjectViewModelDesign();
       StageClaimViewModel = new StageClaimViewModelDesign();
       TargetAmount = new AmountUI(project.TargetAmount);
       RaisedAmount = new AmountUI(project.Raised());
       IsUnfunded = project.IsUnfunded();
       
       // Execute load command to fetch statistics
       Load.Execute().Subscribe();
    }
    
    public IEnhancedCommand<Result<IProjectStatisticsViewModel>> Load { get; }
    public IProjectViewModel ProjectViewModel { get; }
    public IProjectStatisticsViewModel ProjectStatisticsViewModel => projectStatisticsViewModelHelper.Value;
    public IStageClaimViewModel StageClaimViewModel { get; }
    public IUnfundedProjectViewModel UnfundedProjectViewModel { get; }
    public IAmountUI RaisedAmount { get; }
    public IAmountUI TargetAmount { get; }
    public bool IsUnfunded { get; }

    public void Dispose()
    {
        Load.Dispose();
    }
}