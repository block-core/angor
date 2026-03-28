using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Shared;
using Avalonia.Media.Imaging;
using App.UI.Sections.Portfolio;
using App.UI.Shared;
using App.UI.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace App.UI.Sections.FindProjects;

/// <summary>
/// Project data model for the UI. When SDK data is available, mapped from ProjectDto.
/// </summary>
public class ProjectItemViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string ProjectName { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string Description { get; set; } = "";

    private int _investorCount;
    public int InvestorCount
    {
        get => _investorCount;
        set { _investorCount = value; OnPropertyChanged(); }
    }

    public string InvestorLabel { get; set; } = "Investors";

    private string _raised = "0.00000";
    public string Raised
    {
        get => _raised;
        set { _raised = value; OnPropertyChanged(); }
    }

    public string Target { get; set; } = "0.00000";
    public string TargetLabel { get; set; } = "Target:";

    private double _progress;
    public double Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public string ProjectType { get; set; } = "Invest";
    public string Status { get; set; } = "Open";

    private string? _bannerUrl;
    public string? BannerUrl
    {
        get => _bannerUrl;
        set
        {
            _bannerUrl = value;
            ImageCacheService.LoadBitmapAsync(value, bmp => { BannerBitmap = bmp; OnPropertyChanged(nameof(BannerBitmap)); });
        }
    }

    private string? _avatarUrl;
    public string? AvatarUrl
    {
        get => _avatarUrl;
        set
        {
            _avatarUrl = value;
            ImageCacheService.LoadBitmapAsync(value, bmp => { AvatarBitmap = bmp; OnPropertyChanged(nameof(AvatarBitmap)); });
        }
    }

    public Bitmap? BannerBitmap { get; private set; }
    public Bitmap? AvatarBitmap { get; private set; }
    public string ProjectId { get; set; } = "";
    public string FounderKey { get; set; } = "";
    public string NostrNpub { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string ExpiryDate { get; set; } = "";
    public string PenaltyDays { get; set; } = "30";
    public string PayoutFrequency { get; set; } = "Monthly";
    public long SubscriptionPrice { get; set; } = 20000;

    /// <summary>Formatted subscription price display, e.g. "0.0002 BTC"</summary>
    public string SubscriptionPriceDisplay => $"{SubscriptionPrice / 100_000_000.0:G} {CurrencySymbol}";

    public ObservableCollection<InvestmentStageViewModel> Stages { get; set; } = new();

    private Shared.ProjectType TypeEnum => ProjectTypeExtensions.FromDisplayString(ProjectType);
    public string OpportunityTitle => ProjectTypeTerminology.OpportunityTitle(TypeEnum);
    public string ActionButtonText => ProjectTypeTerminology.ActionButtonText(TypeEnum);
    public string InfoSectionTitle => ProjectTypeTerminology.InfoSectionTitle(TypeEnum);
    public string InvestorNoun => ProjectTypeTerminology.InvestorNounTotal(TypeEnum);
    public string TargetNoun => ProjectTypeTerminology.TargetNoun(TypeEnum);
    public string RaisedNoun => ProjectTypeTerminology.RaisedNoun(TypeEnum);

    public bool IsInvestmentType => ProjectType == "Invest";
    public bool IsFundType => ProjectType == "Fund";
    public bool IsSubscriptionType => ProjectType == "Subscription";
    public bool IsOpen => Status == "Open";
    public bool IsFunded => Status == "Funded";
    public bool IsFundingClosed => Status == "Funding Closed";

    /// <summary>Currency symbol for display (e.g. "BTC", "TBTC")</summary>
    public string CurrencySymbol { get; set; } = "BTC";

    /// <summary>
    /// Map an SDK ProjectDto to a UI ProjectItemViewModel.
    /// </summary>
    public static ProjectItemViewModel FromDto(ProjectDto dto)
    {
        var targetBtc = dto.TargetAmount / 100_000_000.0;
        var projectType = dto.ProjectType switch
        {
            Angor.Shared.Models.ProjectType.Fund => "Fund",
            Angor.Shared.Models.ProjectType.Subscribe => "Subscription",
            _ => "Invest"
        };
        var investorLabel = dto.ProjectType switch
        {
            Angor.Shared.Models.ProjectType.Fund => "Funders",
            Angor.Shared.Models.ProjectType.Subscribe => "Subscribers",
            _ => "Investors"
        };
        var targetLabel = dto.ProjectType switch
        {
            Angor.Shared.Models.ProjectType.Fund => "Goal:",
            Angor.Shared.Models.ProjectType.Subscribe => "Total Subscribers:",
            _ => "Target:"
        };

        var stages = new ObservableCollection<InvestmentStageViewModel>();
        if (dto.Stages != null)
        {
            foreach (var stage in dto.Stages)
            {
                stages.Add(new InvestmentStageViewModel
                {
                    StageNumber = stage.Index + 1,
                    Percentage = $"{stage.RatioOfTotal * 100:F0}%",
                    ReleaseDate = stage.ReleaseDate.ToString("dd MMM yyyy"),
                    Amount = (stage.Amount / 100_000_000.0).ToString("F8", CultureInfo.InvariantCulture),
                    Status = "Pending"
                });
            }
        }

        var vm = new ProjectItemViewModel
        {
            ProjectName = dto.Name ?? "Untitled Project",
            ShortDescription = dto.ShortDescription ?? "",
            Description = dto.ShortDescription ?? "",
            Target = targetBtc.ToString("F5", CultureInfo.InvariantCulture),
            TargetLabel = targetLabel,
            ProjectType = projectType,
            InvestorLabel = investorLabel,
            Status = "Open",
            ProjectId = dto.Id?.Value ?? "",
            FounderKey = dto.FounderPubKey ?? "",
            NostrNpub = dto.NostrNpubKeyHex ?? "",
            StartDate = dto.FundingStartDate.ToString("dd MMM yyyy"),
            EndDate = dto.FundingEndDate.ToString("dd MMM yyyy"),
            PenaltyDays = dto.PenaltyDuration.Days.ToString(),
            Stages = stages,
            BannerUrl = dto.Banner?.ToString(),
            AvatarUrl = dto.Avatar?.ToString()
        };

        return vm;
    }
}

/// <summary>
/// FindProjects ViewModel — connected to SDK for project discovery.
/// Falls back to sample data if SDK call fails.
/// </summary>
public partial class FindProjectsViewModel : ReactiveObject
{
    private readonly IProjectAppService _projectAppService;
    private readonly Func<ProjectItemViewModel, InvestPageViewModel> _investPageFactory;
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<FindProjectsViewModel> _logger;

    /// <summary>Currency symbol from ICurrencyService (e.g. "BTC", "TBTC").</summary>
    public string CurrencySymbol => _currencyService.Symbol;

    [Reactive] private ProjectItemViewModel? selectedProject;
    [Reactive] private InvestPageViewModel? investPageViewModel;
    [Reactive] private bool isLoading;

    public void OpenProjectDetail(ProjectItemViewModel project)
    {
        _logger.LogInformation("Opening project detail: '{ProjectName}' (ID: {ProjectId})", project.ProjectName, project.ProjectId);
        SelectedProject = project;
    }

    public void CloseProjectDetail()
    {
        _logger.LogInformation("Closing project detail");
        SelectedProject = null;
    }

    public void OpenInvestPage()
    {
        if (SelectedProject == null) return;
        _logger.LogInformation("Opening invest page for project '{ProjectName}' (ID: {ProjectId})",
            SelectedProject.ProjectName, SelectedProject.ProjectId);
        InvestPageViewModel = _investPageFactory(SelectedProject);
    }

    public void CloseInvestPage()
    {
        _logger.LogInformation("Closing invest page");
        InvestPageViewModel = null;
    }

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = new();

    public FindProjectsViewModel(
        IProjectAppService projectAppService,
        Func<ProjectItemViewModel, InvestPageViewModel> investPageFactory,
        ICurrencyService currencyService,
        ILogger<FindProjectsViewModel> logger)
    {
        _projectAppService = projectAppService;
        _investPageFactory = investPageFactory;
        _currencyService = currencyService;
        _logger = logger;

        _logger.LogInformation("FindProjectsViewModel created");

        // Load projects from SDK
        _ = LoadProjectsFromSdkAsync();
    }

    /// <summary>
    /// Load latest projects from the SDK (Nostr relays).
    /// Falls back to empty list on failure.
    /// </summary>
    public async Task LoadProjectsFromSdkAsync()
    {
        IsLoading = true;
        _logger.LogInformation("Loading latest projects from SDK...");

        try
        {
            var result = await _projectAppService.Latest(new LatestProjects.LatestProjectsRequest());

            if (result.IsSuccess)
            {
                Projects.Clear();
                foreach (var dto in result.Value.Projects)
                {
                    var vm = ProjectItemViewModel.FromDto(dto);
                    vm.CurrencySymbol = _currencyService.Symbol;
                    Projects.Add(vm);
                }
                _logger.LogInformation("Loaded {Count} project(s) from SDK", Projects.Count);

                // Fire-and-forget statistics loading for each project
                _ = LoadProjectStatisticsAsync();
            }
            else
            {
                _logger.LogWarning("Latest projects request failed: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading projects from SDK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Fetch project statistics (investor count, raised amount) for all loaded projects.
    /// Updates each ProjectItemViewModel in-place as results arrive.
    /// </summary>
    private async Task LoadProjectStatisticsAsync()
    {
        var tasks = Projects
            .Where(p => !string.IsNullOrEmpty(p.ProjectId))
            .Select(async project =>
            {
                try
                {
                    var statsResult = await _projectAppService.GetProjectStatistics(
                        new ProjectId(project.ProjectId));

                    if (statsResult.IsSuccess)
                    {
                        var stats = statsResult.Value;
                        var raisedBtc = stats.TotalInvested / 100_000_000.0;
                        project.Raised = raisedBtc.ToString("F5", CultureInfo.InvariantCulture);
                        project.InvestorCount = stats.TotalInvestors ?? 0;

                        // Compute progress as percentage of target
                        if (double.TryParse(project.Target, NumberStyles.Float, CultureInfo.InvariantCulture, out var target) && target > 0)
                        {
                            project.Progress = Math.Min(raisedBtc / target * 100, 100);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("GetProjectStatistics failed for {ProjectId}: {Error}", project.ProjectId, statsResult.Error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error loading statistics for project {ProjectId}", project.ProjectId);
                }
            });

        await Task.WhenAll(tasks);
    }
}
