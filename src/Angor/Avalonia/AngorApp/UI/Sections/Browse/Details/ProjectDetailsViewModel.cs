using Angor.Contexts.Funding.Shared;
using AngorApp.Core.Factories;
using AngorApp.UI.Shared.Controls.Common.FoundedProjectOptions;

namespace AngorApp.UI.Sections.Browse.Details;

public class ProjectDetailsViewModel : ReactiveObject, IProjectDetailsViewModel
{
    private readonly FullProject project;
    private bool enableProductionValidations;

    public ProjectDetailsViewModel(
        FullProject project,
        IProjectInvestCommandFactory investCommandFactory,
        Func<ProjectId, IFoundedProjectOptionsViewModel> foundedProjectOptionsFactory,
        UIServices uiServices)
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

        Invest = investCommandFactory.Create(project, IsInsideInvestmentPeriod);
        FoundedProjectOptions = foundedProjectOptionsFactory(project.ProjectId);
    }

    public bool IsInsideInvestmentPeriod { get; }
    public TimeSpan? NextRelease { get; }
    public IStage? CurrentStage { get; }
    public IFoundedProjectOptionsViewModel FoundedProjectOptions { get; }

    public IEnhancedCommand<Result<Maybe<Unit>>> Invest { get; }

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