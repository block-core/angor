using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Projects.Operations;
using Avalonia.Media.Imaging;
using App.UI.Sections.Portfolio;
using App.UI.Shared;
using App.UI.Shared.Helpers;

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
    public int InvestorCount { get; set; }
    public string InvestorLabel { get; set; } = "Investors";
    public string Raised { get; set; } = "0.00000";
    public string Target { get; set; } = "0.00000";
    public string TargetLabel { get; set; } = "Target:";
    public double Progress { get; set; }
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

    /// <summary>Currency symbol from ICurrencyService (e.g. "BTC", "TBTC").</summary>
    public string CurrencySymbol => _currencyService.Symbol;

    [Reactive] private ProjectItemViewModel? selectedProject;
    [Reactive] private InvestPageViewModel? investPageViewModel;
    [Reactive] private bool isLoading;

    public void OpenProjectDetail(ProjectItemViewModel project) => SelectedProject = project;
    public void CloseProjectDetail() => SelectedProject = null;

    public void OpenInvestPage()
    {
        if (SelectedProject == null) return;
        InvestPageViewModel = _investPageFactory(SelectedProject);
    }

    public void CloseInvestPage() => InvestPageViewModel = null;

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = new();

    public FindProjectsViewModel(
        IProjectAppService projectAppService,
        Func<ProjectItemViewModel, InvestPageViewModel> investPageFactory,
        ICurrencyService currencyService)
    {
        _projectAppService = projectAppService;
        _investPageFactory = investPageFactory;
        _currencyService = currencyService;

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
            }
        }
        catch
        {
            // SDK call failed - projects list remains empty
        }
        finally
        {
            IsLoading = false;
        }
    }
}
