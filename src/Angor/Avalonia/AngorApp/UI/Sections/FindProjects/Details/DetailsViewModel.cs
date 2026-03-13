using System.Reactive.Linq;
using System.Text.Json;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Operations;
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
            var json = await SerializeProjectInfoAsync(project, projectAppService);
            await dialog.Show(new LongTextViewModel { Text = json }, "Project Info (JSON)", Observable.Return(true));
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

    private static async Task<string> SerializeProjectInfoAsync(IProject project, IProjectAppService projectAppService)
    {
        try
        {
            var info = project switch
            {
                IInvestmentProject investmentProject => await SerializeInvestmentProjectInfo(investmentProject, projectAppService),
                IFundProject fundProject => await SerializeFundProjectInfo(fundProject, projectAppService),
                _ => SerializeUnsupportedProjectInfo(project)
            };

            return JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error serializing project info: {ex.Message}";
        }
    }

    private static async Task<object> SerializeInvestmentProjectInfo(IInvestmentProject project, IProjectAppService projectAppService)
    {
        var projectResult = await projectAppService.TryGet(new TryGetProject.TryGetProjectRequest(project.Id));
        var stages = projectResult.IsSuccess ? projectResult.Value.Project.GetValueOrDefault()?.Stages : null;
        var statsResult = await projectAppService.GetProjectStatistics(project.Id);

        return new
        {
            Type = "investment",
            SnapshotPolicy = "local",
            Project = SerializeCommonProjectInfo(project),
            Investment = new
            {
                Target = SerializeAmount(project.Target),
                FundingStart = project.FundingStart,
                FundingEnd = project.FundingEnd,
                PenaltyDuration = project.PenaltyDuration.ToString(),
                PenaltyThreshold = SerializeAmount(project.PenaltyThreshold),
                TotalInvested = statsResult.IsSuccess ? statsResult.Value.TotalInvested : (long?)null,
                TotalInvestors = statsResult.IsSuccess ? statsResult.Value.TotalInvestors : null,
                Stages = stages?.Select(stage => new
                {
                    stage.Index,
                    stage.ReleaseDate,
                    stage.Amount,
                    stage.RatioOfTotal,
                })
            }
        };
    }

    private static async Task<object> SerializeFundProjectInfo(IFundProject project, IProjectAppService projectAppService)
    {
        var statsResult = await projectAppService.GetProjectStatistics(project.Id);
        var dynamicStages = statsResult.IsSuccess ? statsResult.Value.DynamicStages : null;

        return new
        {
            Type = "fund",
            SnapshotPolicy = "local",
            Project = SerializeCommonProjectInfo(project),
            Fund = new
            {
                Goal = SerializeAmount(project.Goal),
                project.TransactionDate,
                TotalInvested = statsResult.IsSuccess ? statsResult.Value.TotalInvested : (long?)null,
                TotalInvestors = statsResult.IsSuccess ? statsResult.Value.TotalInvestors : null,
                DynamicStages = dynamicStages?.Select(stage => new
                {
                    stage.StageIndex,
                    stage.ReleaseDate,
                    stage.TotalAmount,
                    stage.TransactionCount,
                    stage.UnspentTransactionCount,
                    stage.UnspentAmount,
                    stage.IsReleased,
                    stage.Status,
                })
            }
        };
    }

    private static object SerializeUnsupportedProjectInfo(IProject project)
    {
        return new
        {
            Type = "unsupported",
            SnapshotPolicy = "local",
            Message = $"Unsupported project type '{project.GetType().Name}'.",
            Project = SerializeCommonProjectInfo(project)
        };
    }

    private static object SerializeCommonProjectInfo(IProject project)
    {
        return new
        {
            ProjectId = project.Id.Value,
            project.Name,
            Description = project.Description,
            project.FounderPubKey,
            project.NostrNpubKeyHex,
            BannerUrl = project.BannerUrl?.ToString(),
            LogoUrl = project.LogoUrl?.ToString(),
            InformationUrl = project.InformationUri?.ToString(),
        };
    }

    private static object? SerializeAmount(IAmountUI? amount)
    {
        return amount is null
            ? null
            : new
            {
                amount.Sats,
                amount.Btc,
                amount.DecimalString,
                amount.ShortDecimalString,
            };
    }
}
