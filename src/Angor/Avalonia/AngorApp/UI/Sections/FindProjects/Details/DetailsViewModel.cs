using System.Text.Json;
using AngorApp.Model.Common;
using AngorApp.UI.Flows.InvestV2;
using Nostr.Client.Utils;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.FindProjects.Details;

public class DetailsViewModel : ReactiveObject, IDetailsViewModel
{
    public DetailsViewModel(IFullProject project, Func<IFullProject, IInvestViewModel> investViewModelFactory, INavigator navigator, IDialog dialog)
    {
        Project = project;
        Invest = EnhancedCommand.CreateWithResult(() => navigator.Go(() => investViewModelFactory(project))).AsResult();
        ShowProjectInfoJson = ReactiveCommand.CreateFromTask(() => dialog.Show(new LongTextViewModel
        {
            Text = SerializeProjectInfo(project),
        }, "Project Info (JSON)", System.Reactive.Linq.Observable.Return(true))).Enhance();
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
    public IEnhancedCommand ShowProjectInfoJson { get; }

    private static string SerializeProjectInfo(IFullProject project)
    {
        try
        {
            var info = new
            {
                ProjectId = project.ProjectId?.Value,
                project.Name,
                project.ShortDescription,
                ProjectType = project.ProjectType.ToString(),
                project.Version,
                FounderPubKey = project.FounderPubKey,
                NostrNpubKeyHex = project.NostrNpubKeyHex,
                TargetAmount = new { project.TargetAmount.Sats, Btc = project.TargetAmount.Btc },
                RaisedAmount = new { project.RaisedAmount.Sats, Btc = project.RaisedAmount.Btc },
                TotalInvestors = project.TotalInvestors,
                FundingStartDate = project.FundingStartDate,
                FundingEndDate = project.FundingEndDate,
                PenaltyDuration = project.PenaltyDuration.ToString(),
                PenaltyThreshold = project.PenaltyThreshold != null ? new { project.PenaltyThreshold.Sats, Btc = project.PenaltyThreshold.Btc } : null,
                Stages = project.Stages?.Select(s => new { s.ReleaseDate, s.RatioOfTotal, s.Amount, s.Index }),
                DynamicStagePatterns = project.DynamicStagePatterns,
                DynamicStages = project.DynamicStages,
                Status = project.Status.ToString(),
            };

            return JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error serializing project info: {ex.Message}";
        }
    }
}
