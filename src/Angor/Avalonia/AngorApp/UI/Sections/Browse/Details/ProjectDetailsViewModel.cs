using Angor.Sdk.Funding.Shared;
using AngorApp.Core.Factories;
using AngorApp.UI.Flows.InvestV2;
using AngorApp.UI.Shared.Controls.Common.FoundedProjectOptions;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.Browse.Details;

public class ProjectDetailsViewModel : ReactiveObject, IProjectDetailsViewModel
{
    private readonly IFullProject project;
    private bool enableProductionValidations;

    public ProjectDetailsViewModel(
        IFullProject project,
        Func<ProjectId, IFoundedProjectOptionsViewModel> foundedProjectOptionsFactory,
        Func<IFullProject, IInvestViewModel> investViewModelFactory,
        UIServices uiServices, INavigator navigator)
    {
        this.project = project;

        enableProductionValidations = uiServices.EnableProductionValidations();

        if (enableProductionValidations)
        {
            // todo: when fund and subscribe are implemented there is no limit to investment period
            IsInsideInvestmentPeriod = DateTime.Now <= project.FundingEndDate;
        }
        else
        {
            IsInsideInvestmentPeriod = true;
        }
        
        Invest = EnhancedCommand.CreateWithResult(() => navigator.Go(() => investViewModelFactory(project)) , Observable.Return(IsInsideInvestmentPeriod));
        Invest.HandleErrorsWith(uiServices.NotificationService, "Investment failed");
        
        FoundedProjectOptions = foundedProjectOptionsFactory(project.ProjectId);
    }

    public bool IsInsideInvestmentPeriod { get; }
    public TimeSpan? NextRelease { get; }
    public IStage? CurrentStage { get; }
    public IFoundedProjectOptionsViewModel FoundedProjectOptions { get; }

    public IEnhancedCommand<Result<Unit>> Invest { get; }

    public IEnumerable<INostrRelay> Relays { get; } =
    [
        new NostrRelaySample
        {
            Uri = new Uri("wss://relay.angor.io")
        },
        new NostrRelaySample
        {
            Uri = new Uri("wss://relay2.angor.io")
        }
    ];

    public IFullProject Project => project;
}