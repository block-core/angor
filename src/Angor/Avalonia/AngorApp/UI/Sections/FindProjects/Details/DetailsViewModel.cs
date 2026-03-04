using System.Text.Json;
using Angor.Sdk.Funding.Projects;
using Angor.Shared;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using Nostr.Client.Utils;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Sections.FindProjects.Details;

public class DetailsViewModel : ReactiveObject, IDetailsViewModel
{
    private readonly string primaryExplorerUrl;

    public DetailsViewModel(IProject project, IProjectAppService projectAppService, IDialog dialog, INetworkStorage networkStorage)
    {
        var indexers = networkStorage.GetSettings().Indexers;
        var primary = indexers.FirstOrDefault(e => e.IsPrimary) ?? indexers.FirstOrDefault();
        primaryExplorerUrl = primary?.Url?.TrimEnd('/') ?? "https://mempool.space";

        Project = project;
        ShowProjectInfoJson = ReactiveCommand.CreateFromTask(async () =>
        {
            var fullProject = await projectAppService.GetFullProject(project.Id);
            if (fullProject.IsSuccess)
            {
                var json = SerializeProjectInfo(fullProject.Value);
                await dialog.Show(new LongTextViewModel { Text = json }, "Project Info (JSON)", Observable.Return(true));
            }
        }).Enhance();
    }

    public IEnhancedCommand<Result> Invest => Project.Invest;

    public IProject Project { get; }

    public bool IsInsideInvestmentPeriod =>
        Project is not IInvestmentProject investmentProject ||
        (DateTimeOffset.UtcNow >= investmentProject.FundingStart &&
         DateTimeOffset.UtcNow <= investmentProject.FundingEnd);

    public string FounderKey => Project.FounderPubKey ?? "[Backend: FounderPubKey is null]";
    public string ProjectId => Project.Id.Value ?? "[Backend: ProjectId is null]";
    public string ExplorerUrl => Project.Id.Value is { } id
        ? $"{primaryExplorerUrl}/tx/{id}"
        : "[Backend: ProjectId is null]";

    public string NostrNpub => Project.NostrNpubKeyHex is { } hex
        ? NostrConverter.ToNpub(hex) ?? "[Error: Invalid hex for npub conversion]"
        : "[Backend: NostrNpubKeyHex is null]";
    public string NostrHex => Project.NostrNpubKeyHex ?? "[Backend: NostrNpubKeyHex is null]";
    public IEnumerable<string> Relays => new[] { "[TODO: Backend - Needs IProject.Relays]" };
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
