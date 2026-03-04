using AngorApp.Model.Funded.Investment.Samples;
using AngorApp.Model.ProjectsV2;

namespace AngorApp.UI.Sections.FindProjects.Details;

public class DetailsViewModelSample : IDetailsViewModel
{
    public DetailsViewModelSample()
    {
        Project = new InvestmentProjectSample();
    }

    public IProject Project { get; }

    public bool IsInsideInvestmentPeriod => true;

    // Project Details
    public string FounderKey => "ca6e84aa974d00af805a754b34bc4e3c9a899aac14487a6f2e21fe9ea4b9fe43";
    public string ProjectId => "angor1q...";
    public string ExplorerUrl => "#";

    // Nostr
    public string NostrNpub => "npub1...";
    public string NostrHex => "ca6e84aa974d00af805a754b34bc4e3c9a899aac14487a6f2e21fe9ea4b9fe43";
    public IEnumerable<string> Relays => new[] { "wss://relay.angor.io", "wss://relay.primal.net" };
    public IEnhancedCommand<Result> Invest { get; } = EnhancedCommand.CreateWithResult(Result.Success);
    public IEnhancedCommand ShowProjectInfoJson { get; } = EnhancedCommand.Create(() => { });
}
