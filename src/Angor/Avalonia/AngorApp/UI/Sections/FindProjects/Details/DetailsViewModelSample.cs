using AngorApp.Model.Contracts.Projects;
using AngorApp.UI.Shared.Samples;

namespace AngorApp.UI.Sections.FindProjects.Details;

public class DetailsViewModelSample : IDetailsViewModel
{
    public DetailsViewModelSample()
    {
        Project = new FullProjectSample();
    }

    public IFullProject Project { get; }
    public string Name => Project.Name;
    public string ShortDescription => Project.ShortDescription;
    public IAmountUI TargetAmount => Project.TargetAmount;
    public IAmountUI TotalRaised => Project.RaisedAmount;
    public int InvestorsCount => Project.TotalInvestors ?? 0;

    public double FundingProgress
    {
        get
        {
            if (Project.TargetAmount.Sats == 0)
                return 0;
            return (double)Project.RaisedAmount.Sats / Project.TargetAmount.Sats;
        }
    }

    public bool IsInsideInvestmentPeriod => true;

    // Project Statistics
    public IAmountUI TargetAmountBtc => Project.TargetAmount;

    // Investment Information
    public IAmountUI SubscriptionPrice => AmountUI.FromBtc(0.0002m);
    public string Frequency => "Monthly";
    public string Installments => "3 / 6 / 12";

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
