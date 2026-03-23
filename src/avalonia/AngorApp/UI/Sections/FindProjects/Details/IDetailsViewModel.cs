using AngorApp.Model.ProjectsV2;

namespace AngorApp.UI.Sections.FindProjects.Details;

public interface IDetailsViewModel
{
    IProject Project { get; }

    // Investment Opportunity
    bool IsInsideInvestmentPeriod { get; }

    // Project Details
    string FounderKey { get; }
    string ProjectId { get; }
    string ExplorerUrl { get; }

    // Nostr
    string NostrNpub { get; }
    string NostrHex { get; }
    IEnumerable<string> Relays { get; }
    IEnhancedCommand<Result> Invest { get; }
    IEnhancedCommand ShowProjectInfoJson { get; }
}
