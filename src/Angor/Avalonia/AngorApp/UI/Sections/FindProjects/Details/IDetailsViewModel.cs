using AngorApp.Model.Contracts.Projects;

namespace AngorApp.UI.Sections.FindProjects.Details;

public interface IDetailsViewModel
{
    IFullProject Project { get; }
    string Name { get; }
    string ShortDescription { get; }
    IAmountUI TargetAmount { get; }
    IAmountUI TotalRaised { get; }
    int InvestorsCount { get; }
    double FundingProgress { get; }

    // Investment Opportunity
    bool IsInsideInvestmentPeriod { get; }

    // Project Statistics (already partially covered, ensuring specifics)
    IAmountUI TargetAmountBtc { get; }

    // Investment Information
    IAmountUI SubscriptionPrice { get; }
    string Frequency { get; }
    string Installments { get; }

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
