using AngorApp.Model.Domain.Projects;
using AngorApp.UI.Controls.Common.FoundedProjectOptions;

namespace AngorApp.Sections.Browse.Details;

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