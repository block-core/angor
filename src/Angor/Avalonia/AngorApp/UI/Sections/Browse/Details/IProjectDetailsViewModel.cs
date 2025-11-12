using AngorApp.Model.Projects;
using AngorApp.UI.Shared.Controls.Common.FoundedProjectOptions;

namespace AngorApp.UI.Sections.Browse.Details;

public interface IProjectDetailsViewModel
{
    public IEnhancedCommand<Result<Maybe<Unit>>> Invest { get; }
    public IEnumerable<INostrRelay> Relays { get; }
    public IFullProject Project { get; }
    bool IsInsideInvestmentPeriod { get; }
    public TimeSpan? NextRelease { get; }
    public IStage? CurrentStage { get; }
    public IFoundedProjectOptionsViewModel FoundedProjectOptions { get; }
}