using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using Avalonia.Media.Imaging;
using App.UI.Sections.Portfolio;
using App.UI.Shared;
using App.UI.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// Penalty threshold in satoshis from the project's on-chain data.
    /// For Fund-type projects, investments below this amount are auto-approved.
    /// For Invest-type projects, all investments require founder approval regardless.
    /// Null means no threshold was set (treat as 0 → auto-approve all for Fund type).
    /// </summary>
    public long? PenaltyThresholdSats { get; set; }

    /// <summary>Formatted subscription price display, e.g. "0.0002 BTC"</summary>
    public string SubscriptionPriceDisplay => $"{SubscriptionPrice.ToUnitBtc():G} {CurrencySymbol}";

    public ObservableCollection<InvestmentStageViewModel> Stages { get; set; } = new();

    /// <summary>
    /// Dynamic stage patterns from the project (Fund/Subscribe types).
    /// Investors select which pattern to use when funding.
    /// </summary>
    public List<DynamicStagePattern> DynamicStagePatterns { get; set; } = new();

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

    private bool _hasInvested;
    public bool HasInvested
    {
        get => _hasInvested;
        set
        {
            if (_hasInvested == value) return;
            _hasInvested = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOpenAndNotInvested));
        }
    }

    /// <summary>True when the project is open AND the current user has NOT already invested.</summary>
    public bool IsOpenAndNotInvested => IsOpen && !HasInvested;

    /// <summary>Currency symbol for display (e.g. "BTC", "TBTC")</summary>
    public string CurrencySymbol { get; set; } = "BTC";

    /// <summary>
    /// Map an SDK ProjectDto to a UI ProjectItemViewModel.
    /// </summary>
    public static ProjectItemViewModel FromDto(ProjectDto dto)
    {
        var targetBtc = (double)dto.TargetAmount.ToUnitBtc();
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
                    Amount = stage.Amount.ToUnitBtc().ToString("F8", CultureInfo.InvariantCulture),
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
            PenaltyThresholdSats = dto.PenaltyThreshold,
            Stages = stages,
            DynamicStagePatterns = dto.DynamicStagePatterns ?? new List<DynamicStagePattern>(),
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
    /// <summary>
    /// Process-wide cache of the last successful Latest() DTOs.
    /// Populated by CompositionRoot's startup pre-warm and by each successful load.
    /// Lets new VM instances seed their Projects list synchronously so the tab
    /// feels instant even when the ~10s Nostr fetch hasn't completed yet.
    /// </summary>
    internal static IReadOnlyList<ProjectDto>? CachedDtos { get; set; }

    internal const string CacheStoreKey = "findprojects_cache.json";

    /// <summary>
    /// Load persisted DTO cache from disk into <see cref="CachedDtos"/>.
    /// Called from CompositionRoot on startup so the first tap seeds from disk.
    /// </summary>
    internal static async Task LoadCachedDtosFromDiskAsync(IStore store, ILogger logger)
    {
        logger.LogInformation("[FindProjects] disk-load: attempt key={Key}", CacheStoreKey);
        try
        {
            var result = await store.Load<List<ProjectDto>>(CacheStoreKey);
            if (!result.IsSuccess)
            {
                logger.LogInformation("[FindProjects] disk-load: Load() returned failure: {Error}", result.Error);
                return;
            }
            if (result.Value is null)
            {
                logger.LogInformation("[FindProjects] disk-load: result.Value was null");
                return;
            }
            if (result.Value.Count == 0)
            {
                logger.LogInformation("[FindProjects] disk-load: cache file was empty (0 items)");
                return;
            }
            CachedDtos = result.Value;
            logger.LogInformation("[FindProjects] disk-load: SUCCESS loaded {Count} cached projects", result.Value.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[FindProjects] disk-load: threw {ExType}: {ExMsg}", ex.GetType().Name, ex.Message);
        }
    }

    /// <summary>
    /// Persist the current <see cref="CachedDtos"/> to disk for the next app launch.
    /// Fire-and-forget; failures are logged and swallowed.
    /// </summary>
    internal static async Task SaveCachedDtosToDiskAsync(IStore store, IReadOnlyList<ProjectDto> dtos, ILogger logger)
    {
        logger.LogInformation("[FindProjects] disk-save: attempt count={Count} key={Key}", dtos.Count, CacheStoreKey);
        try
        {
            var result = await store.Save(CacheStoreKey, dtos.ToList());
            if (result.IsSuccess)
                logger.LogInformation("[FindProjects] disk-save: SUCCESS wrote {Count} projects", dtos.Count);
            else
                logger.LogWarning("[FindProjects] disk-save: Save() returned failure: {Error}", result.Error);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[FindProjects] disk-save: threw {ExType}: {ExMsg}", ex.GetType().Name, ex.Message);
        }
    }

    private readonly IProjectAppService _projectAppService;
    private readonly Func<ProjectItemViewModel, InvestPageViewModel> _investPageFactory;
    private readonly PortfolioViewModel _portfolioViewModel;
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
        PortfolioViewModel portfolioViewModel,
        ICurrencyService currencyService,
        ILogger<FindProjectsViewModel> logger)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _projectAppService = projectAppService;
        _investPageFactory = investPageFactory;
        _portfolioViewModel = portfolioViewModel;
        _currencyService = currencyService;
        _logger = logger;

        var fieldsMs = sw.ElapsedMilliseconds;
        _logger.LogInformation("FindProjectsViewModel created");

        sw.Restart();
        // When the portfolio investments change, re-check HasInvested flags
        _portfolioViewModel.Investments.CollectionChanged += (_, _) => UpdateHasInvestedFlags();
        var subscribeMs = sw.ElapsedMilliseconds;

        sw.Restart();
        // Seed Projects from the process-wide cache if the startup pre-warm (or a
        // previous VM instance) already fetched Latest(). User sees results instantly
        // on tab-switch; background refresh follows.
        //
        // On first load we only show the first page; the rest are held in
        // _pendingItems and revealed by LoadMore() as the user scrolls.
        var seeded = 0;
        if (CachedDtos is { Count: > 0 } seed)
        {
            var mapped = new List<ProjectItemViewModel>(seed.Count);
            foreach (var dto in seed)
            {
                var vm = ProjectItemViewModel.FromDto(dto);
                vm.CurrencySymbol = _currencyService.Symbol;
                mapped.Add(vm);
            }
            SeedPaged(mapped);
            UpdateHasInvestedFlags();
            seeded = mapped.Count;
        }
        var seedMs = sw.ElapsedMilliseconds;

        sw.Restart();
        // Run load on threadpool so the synchronous prefix of
        // _projectAppService.Latest() doesn't block the UI thread.
        // LoadProjectsFromSdkAsync marshals its UI mutations back via Dispatcher.
        _ = Task.Run(LoadProjectsFromSdkAsync);
        var loadKickMs = sw.ElapsedMilliseconds;

        _logger.LogInformation(
            "[FindProjectsViewModel.ctor] fields={Fields}ms subscribe={Subscribe}ms seeded={Seeded} seed={SeedMs}ms loadKick={LoadKick}ms",
            fieldsMs, subscribeMs, seeded, seedMs, loadKickMs);
    }

    /// <summary>
    /// Load latest projects from the SDK (Nostr relays).
    /// Falls back to empty list on failure.
    /// </summary>
    public async Task LoadProjectsFromSdkAsync()
    {
        var pl = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ProjectsLoad");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        pl.LogInformation("[ProjectsLoad] t=0ms begin");

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = true);
        pl.LogInformation("[ProjectsLoad] t={T}ms IsLoading=true", sw.ElapsedMilliseconds);

        // Watchdog: warn every 3s if Latest() still hasn't returned.
        using var cts = new CancellationTokenSource();
        var watchdog = Task.Run(async () =>
        {
            int tick = 0;
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(3000, cts.Token);
                    tick++;
                    pl.LogWarning("[ProjectsLoad] t={T}ms still awaiting Latest() tick={Tick}",
                        sw.ElapsedMilliseconds, tick);
                }
            }
            catch (OperationCanceledException) { }
        });

        try
        {
            pl.LogInformation("[ProjectsLoad] t={T}ms calling Latest()", sw.ElapsedMilliseconds);
            var callSw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _projectAppService.Latest(new LatestProjects.LatestProjectsRequest());
            callSw.Stop();
            cts.Cancel();

            pl.LogInformation("[ProjectsLoad] t={T}ms Latest() returned in {CallMs}ms success={Success}",
                sw.ElapsedMilliseconds, callSw.ElapsedMilliseconds, result.IsSuccess);

            if (result.IsSuccess)
            {
                var dtos = result.Value.Projects;
                var dtoCount = dtos.Count();
                pl.LogInformation("[ProjectsLoad] t={T}ms got {Count} dtos", sw.ElapsedMilliseconds, dtoCount);

                // Update process-wide seed cache so the next VM instance (e.g. after
                // tab re-open with a fresh DI-scoped transient) shows data instantly.
                var cachedCopy = dtos.ToList();
                CachedDtos = cachedCopy;

                // Persist to disk (fire-and-forget) so next app launch shows projects
                // instantly while the SDK fetch runs in background.
                _ = SaveCachedDtosToDiskAsync(
                    App.Services.GetRequiredService<IStore>(),
                    cachedCopy,
                    App.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FindProjectsCache"));

                var mapSw = System.Diagnostics.Stopwatch.StartNew();
                var items = new List<ProjectItemViewModel>(dtoCount);
                foreach (var dto in dtos)
                {
                    var vm = ProjectItemViewModel.FromDto(dto);
                    vm.CurrencySymbol = _currencyService.Symbol;
                    items.Add(vm);
                }
                mapSw.Stop();
                pl.LogInformation("[ProjectsLoad] t={T}ms mapped {Count} items in {MapMs}ms",
                    sw.ElapsedMilliseconds, items.Count, mapSw.ElapsedMilliseconds);

                var uiSw = System.Diagnostics.Stopwatch.StartNew();
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Re-seed paged: show first page immediately, hold rest in _pendingItems
                    SeedPaged(items);
                    UpdateHasInvestedFlags();
                });
                uiSw.Stop();
                pl.LogInformation("[ProjectsLoad] t={T}ms UI update {UiMs}ms Projects.Count={PC}",
                    sw.ElapsedMilliseconds, uiSw.ElapsedMilliseconds, Projects.Count);

                // Fire-and-forget statistics loading for each project
                _ = LoadProjectStatisticsAsync();
            }
            else
            {
                pl.LogWarning("[ProjectsLoad] t={T}ms Latest() failed: {Error}",
                    sw.ElapsedMilliseconds, result.Error);
            }
        }
        catch (Exception ex)
        {
            cts.Cancel();
            pl.LogError(ex, "[ProjectsLoad] t={T}ms exception: {ExType}: {ExMsg}",
                sw.ElapsedMilliseconds, ex.GetType().Name, ex.Message);
            if (ex.InnerException != null)
                pl.LogError(ex.InnerException, "[ProjectsLoad] inner: {ExType}: {ExMsg}",
                    ex.InnerException.GetType().Name, ex.InnerException.Message);
        }
        finally
        {
            cts.Cancel();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
            pl.LogInformation("[ProjectsLoad] t={T}ms DONE", sw.ElapsedMilliseconds);
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
                        var raisedBtc = (double)stats.TotalInvested.ToUnitBtc();
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

    /// <summary>
    /// Cross-reference loaded projects with the user's portfolio investments
    /// to set HasInvested flags on each ProjectItemViewModel.
    /// </summary>
    private void UpdateHasInvestedFlags()
    {
        var investedProjectIds = _portfolioViewModel.Investments
            .Select(i => i.ProjectIdentifier)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var project in Projects)
        {
            project.HasInvested = investedProjectIds.Contains(project.ProjectId);
        }

        _logger.LogDebug("Updated HasInvested flags: {InvestedCount} of {TotalCount} projects marked as invested",
            Projects.Count(p => p.HasInvested), Projects.Count);
    }

    /// <summary>
    /// Number of cards revealed per page. Mobile uses a small batch (4) paired
    /// with an explicit "Load More" button so users control reveal pacing and
    /// we never stutter-inflate a long list during a scroll flick. Desktop
    /// keeps the larger batch — the viewport has room and no auto-scroll
    /// trigger runs there either.
    /// </summary>
    public static int PageSize =>
        (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS()) ? 4 : 12;

    private readonly List<ProjectItemViewModel> pendingItems = new();

    /// <summary>
    /// True when there are more items waiting to be revealed via <see cref="LoadMore"/>.
    /// Bound by the view to show/hide the "Load More" button.
    /// </summary>
    [Reactive] private bool hasMoreItems;

    /// <summary>
    /// True while a <see cref="LoadMore"/> batch is being drained. Used by the
    /// view to disable the Load More button and show a spinner so repeated
    /// taps don't pile up concurrent layout invalidations.
    /// </summary>
    [Reactive] private bool isLoadingMore;

    /// <summary>
    /// Replace <see cref="Projects"/> with the first <see cref="PageSize"/> items from
    /// <paramref name="all"/>, and stash the remainder in <see cref="pendingItems"/>
    /// for <see cref="LoadMore"/> to reveal when the user taps the "Load More"
    /// button. Must be called on the UI thread.
    ///
    /// Both mobile (PageSize=4) and desktop (PageSize=12) inflate the initial
    /// page synchronously. The mobile batch is small enough that there's no
    /// stagger benefit, and synchronous inflate guarantees visible content on
    /// first frame without any dispatcher fragility.
    /// </summary>
    private void SeedPaged(List<ProjectItemViewModel> all)
    {
        Projects.Clear();
        pendingItems.Clear();

        var initial = Math.Min(PageSize, all.Count);
        for (var i = 0; i < initial; i++)
            Projects.Add(all[i]);

        for (var i = initial; i < all.Count; i++)
            pendingItems.Add(all[i]);

        HasMoreItems = pendingItems.Count > 0;
        _logger.LogInformation("[Paged] seed visible={Visible} pending={Pending} hasMore={HasMore}",
            Projects.Count, pendingItems.Count, HasMoreItems);
    }

    /// <summary>
    /// Reveal the next page of projects. Called by the view when the user taps
    /// the "Load More" button. Safe to call when there are no pending items
    /// (no-op). Toggles <see cref="IsLoadingMore"/> across the insert so the
    /// view can show a spinner and disable the button while the batch drains.
    /// </summary>
    public void LoadMore()
    {
        if (pendingItems.Count == 0) return;
        if (IsLoadingMore) return;
        IsLoadingMore = true;

        var take = Math.Min(PageSize, pendingItems.Count);
        var batch = pendingItems.GetRange(0, take);
        pendingItems.RemoveRange(0, take);
        HasMoreItems = pendingItems.Count > 0;

        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            // Post the whole batch in a single ApplicationIdle tick so the
            // ResponsiveGrid does one layout pass per batch, and the button's
            // spinner gets a frame to paint before we block on inflate.
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var item in batch)
                    Projects.Add(item);
                UpdateHasInvestedFlags();
                IsLoadingMore = false;
            }, Avalonia.Threading.DispatcherPriority.ApplicationIdle);
        }
        else
        {
            foreach (var item in batch)
                Projects.Add(item);
            UpdateHasInvestedFlags();
            IsLoadingMore = false;
        }

        _logger.LogInformation("[Paged] LoadMore revealed={Take} visible={Visible} pending={Pending}",
            take, Projects.Count, pendingItems.Count);
    }
}
