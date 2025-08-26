using Angor.UI.Model.Implementation.Projects;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Browse.Details;

public interface IProjectDetailsViewModel
{
    public IEnhancedCommand<Result> Invest { get; }
    public IEnumerable<INostrRelay> Relays { get; }
    public IFullProject Project { get; }
    bool IsInsideInvestmentPeriod { get; }
}