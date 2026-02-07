using AngorApp.UI.Flows.InvestV2;
using Nostr.Client.Utils;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.FindProjects.Details;

public class DetailsViewModel : ReactiveObject, IDetailsViewModel
{
    public DetailsViewModel(IFullProject project, Func<IFullProject, IInvestViewModel> investViewModelFactory, INavigator navigator)
    {
        Project = project;
        Invest = EnhancedCommand.CreateWithResult(() => navigator.Go(() => investViewModelFactory(project))).AsResult();
    }

    public IEnhancedCommand<Result> Invest { get; set; }

    public IFullProject Project { get; }

    public string Name => Project.Name ?? "[Backend: Name is null]";
    public string ShortDescription => Project.ShortDescription ?? "[Backend: ShortDescription is null]";
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

    // TODO: Backend - Calculate based on FundingStartDate/FundingEndDate
    public bool IsInsideInvestmentPeriod => true;       // TODO: replace by correct value

    public IAmountUI TargetAmountBtc => Project.TargetAmount;

    // TODO: Backend - These properties need to be added to IFullProject
    public IAmountUI SubscriptionPrice => AmountUI.FromBtc(0.0002m); // TODO: Backend - Needs IFullProject.SubscriptionPrice
    public string Frequency => "[TODO: Backend]"; // TODO: Backend - Needs IFullProject.PaymentFrequency
    public string Installments => "[TODO: Backend]"; // TODO: Backend - Needs IFullProject.InstallmentOptions

    public string FounderKey => Project.FounderPubKey ?? "[Backend: FounderPubKey is null]";
    public string ProjectId => Project.ProjectId?.Value ?? "[Backend: ProjectId is null]";
    public string ExplorerUrl => Project.ProjectId?.Value is { } id
        ? $"https://mempool.space/tx/{id}"
        : "[Backend: ProjectId is null]";

    public string NostrNpub => Project.NostrNpubKeyHex is { } hex
        ? NostrConverter.ToNpub(hex) ?? "[Error: Invalid hex for npub conversion]"
        : "[Backend: NostrNpubKeyHex is null]";
    public string NostrHex => Project.NostrNpubKeyHex ?? "[Backend: NostrNpubKeyHex is null]";
    public IEnumerable<string> Relays => new[] { "[TODO: Backend - Needs IFullProject.Relays]" }; // TODO: Backend - Needs relay list from project
}
