using System.Linq;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public class ManageFundsViewModel : ReactiveObject, IManageFundsViewModel
{
    public ManageFundsViewModel(ProjectDto project, IInvestmentAppService appService)
    {
       // TODO: We need to fetch project information from the app service using the Load command.
       Load = ReactiveCommand.Create(() => { }).Enhance();
       ProjectViewModel = new ProjectViewModelDesign();
       ProjectStatisticsViewModel = new ProjectStatisticsViewModel(project);
       UnfundedProjectViewModel = new UnfundedProjectViewModelDesign();
       StageClaimViewModel = new StageClaimViewModelDesign();
       TargetAmount = new AmountUI(project.TargetAmount);
       RaisedAmount = new AmountUI(project.Raised());
       IsUnfunded = project.IsUnfunded();    
    }
    
    public IEnhancedCommand Load { get; }
    public IProjectViewModel ProjectViewModel { get; }
    public IProjectStatisticsViewModel ProjectStatisticsViewModel { get; }
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