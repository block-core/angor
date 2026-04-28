using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using App.UI.Sections.FindProjects;
using App.UI.Sections.MyProjects;
using App.UI.Sections.Portfolio;
using App.UI.Shared;
using App.UI.Shared.Helpers;
using App.UI.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.UI.Shell;

// ICurrencyService is resolved from DI and threaded through to sub-types that need it.

/// <summary>
/// A shared signature/funding request that lives in the SignatureStore.
/// Created when an investor invests, read by Funders (founder side) and
/// Portfolio/InvestmentDetail (investor side).
/// Vue: signatures ref in App.vue (line 7447).
/// </summary>
public class SharedSignature
{
    public int Id { get; set; }
    public string ProjectId { get; set; } = "";
    public string ProjectTitle { get; set; } = "";
    public string Amount { get; set; } = "0.0000";
    public string Currency { get; set; } = "BTC";
    public string Date { get; set; } = "";
    public string Time { get; set; } = "";
    /// <summary>Status: waiting, approved, rejected</summary>
    public string Status { get; set; } = SignatureStatus.Waiting.ToLowerString();
    public string InvestorName { get; set; } = "";
    public string Npub { get; set; } = "";
    public bool HasMessages { get; set; }

    // Helpers
    public bool IsWaiting => Status == SignatureStatus.Waiting.ToLowerString();
    public bool IsApproved => Status == SignatureStatus.Approved.ToLowerString();
    public bool IsRejected => Status == SignatureStatus.Rejected.ToLowerString();
}

/// <summary>
/// Centralized store for signature/funding requests.
/// Shared between Funders (founder view) and Portfolio (investor view).
/// Vue: signatures ref + approveFunder/rejectFunder in App.vue.
/// 
/// Approval logic:
///   - Investment-type projects always require founder approval.
///   - Fund-type projects compare the investment amount (sats) against the
///     project's on-chain PenaltyThreshold. If no threshold or amount below
///     threshold → auto-approved.
/// </summary>
public class SignatureStore
{
    private int _nextId = 100;
    private readonly ICurrencyService _currencyService;

    /// <summary>All signatures across all projects.</summary>
    public ObservableCollection<SharedSignature> AllSignatures { get; } = new();

    /// <summary>Event raised when a signature's status changes (approve/reject).</summary>
    public event Action<SharedSignature>? SignatureStatusChanged;

    public SignatureStore(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    /// <summary>
    /// Add a new signature for an investment.
    /// Vue: handleInvestment() in App.vue (line 8806).
    /// Applies approval logic:
    ///   - Investment-type projects always require founder approval.
    ///   - Fund-type projects compare amount (sats) against the project's PenaltyThreshold.
    /// </summary>
    public SharedSignature AddSignature(string projectId, string projectTitle, string amount, string projectType = "invest", long? penaltyThresholdSats = null)
    {
        var amountValue = double.TryParse(amount, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var a) ? a : 0;

        // Investment-type projects always require founder approval.
        // Fund-type projects compare against the project's on-chain penalty threshold.
        bool requiresApproval;
        if (string.Equals(projectType, "invest", StringComparison.OrdinalIgnoreCase))
        {
            requiresApproval = true;
        }
        else
        {
            var amountSats = ((decimal)amountValue).ToUnitSatoshi();
            var threshold = penaltyThresholdSats ?? 0;
            requiresApproval = threshold > 0 && amountSats >= threshold;
        }

        var now = DateTime.UtcNow;

        var sig = new SharedSignature
        {
            Id = _nextId++,
            ProjectId = projectId,
            ProjectTitle = projectTitle,
            Amount = amount,
            Currency = _currencyService.Symbol,
            Date = now.ToString("dd MMM yyyy"),
            Time = now.ToString("HH:mm"),
            Status = requiresApproval ? SignatureStatus.Waiting.ToLowerString() : SignatureStatus.Approved.ToLowerString(),
            InvestorName = $"Investor {AllSignatures.Count(s => s.ProjectId == projectId) + 1}",
            Npub = $"npub1{Guid.NewGuid():N}{Guid.NewGuid():N}"[..64],
            HasMessages = false
        };

        AllSignatures.Insert(0, sig);
        return sig;
    }

    /// <summary>
    /// Approve a signature. Vue: approveFunder() in App.vue (line 7450).
    /// </summary>
    public void Approve(int id)
    {
        var sig = AllSignatures.FirstOrDefault(s => s.Id == id);
        if (sig == null) return;
        sig.Status = SignatureStatus.Approved.ToLowerString();
        SignatureStatusChanged?.Invoke(sig);
    }

    /// <summary>
    /// Reject a signature. Vue: rejectFunder() in App.vue (line 7461).
    /// </summary>
    public void Reject(int id)
    {
        var sig = AllSignatures.FirstOrDefault(s => s.Id == id);
        if (sig == null) return;
        sig.Status = SignatureStatus.Rejected.ToLowerString();
        SignatureStatusChanged?.Invoke(sig);
    }

    /// <summary>Approve all waiting signatures.</summary>
    public void ApproveAll()
    {
        foreach (var sig in AllSignatures.Where(s => s.Status == SignatureStatus.Waiting.ToLowerString()).ToList())
        {
            sig.Status = SignatureStatus.Approved.ToLowerString();
            SignatureStatusChanged?.Invoke(sig);
        }
    }

    /// <summary>Clear all signatures (used by Reset Data).</summary>
    public void Clear() => AllSignatures.Clear();
}

/// <summary>
/// Global prototype-level settings (e.g. show populated vs empty states).
/// Sections observe ShowPopulatedApp to decide whether to load sample data.
/// </summary>
public partial class PrototypeSettings : ReactiveObject
{
    private readonly IStore _store;
    private const string SettingsKey = "prototype_settings.json";

    /// <summary>
    /// When true, sections show hardcoded sample data (populated state).
    /// When false, sections show empty states.
    /// Default = true so the app starts populated for demos.
    /// </summary>
    [Reactive] private bool showPopulatedApp = true;

    /// <summary>
    /// When true (and on a testnet network), enables debug features:
    /// prepopulate project creation wizard data, bypass some validations, etc.
    /// Synced to <see cref="INetworkConfiguration.SetDebugMode"/>.
    /// </summary>
    [Reactive] private bool isDebugMode;

    /// <summary>
    /// When true, the app uses the Dark theme; otherwise Light.
    /// Persisted to disk so the choice survives restarts.
    /// </summary>
    [Reactive] private bool isDarkTheme;

    /// <summary>
    /// Persisted wallet ID for the currently selected wallet.
    /// Used by <see cref="App.UI.Shared.Services.IWalletContext"/> to restore selection on restart.
    /// </summary>
    [Reactive] private string? selectedWalletId;

    public PrototypeSettings(IStore store)
    {
        _store = store;

        // Load persisted values
        var result = _store.Load<PrototypeSettingsData>(SettingsKey).GetAwaiter().GetResult();
        if (result.IsSuccess)
        {
            isDebugMode = result.Value.IsDebugMode;
            isDarkTheme = result.Value.IsDarkTheme;
            selectedWalletId = result.Value.SelectedWalletId;
        }

        // Apply persisted theme immediately
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = isDarkTheme
                ? Avalonia.Styling.ThemeVariant.Dark
                : Avalonia.Styling.ThemeVariant.Light;
        }

        // Persist on changes
        this.WhenAnyValue(x => x.IsDebugMode, x => x.IsDarkTheme, x => x.SelectedWalletId)
            .Skip(1)
            .Subscribe(async _ =>
            {
                var saveResult = await _store.Save(SettingsKey, new PrototypeSettingsData
                {
                    IsDebugMode = IsDebugMode,
                    IsDarkTheme = IsDarkTheme,
                    SelectedWalletId = SelectedWalletId,
                });
                if (saveResult.IsFailure)
                {
                    var logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(PrototypeSettings));
                    logger.LogWarning("Failed to save settings: {Error}", saveResult.Error);
                }
            });

        // Apply theme whenever IsDarkTheme changes
        this.WhenAnyValue(x => x.IsDarkTheme)
            .Skip(1)
            .Subscribe(dark =>
            {
                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = dark
                        ? Avalonia.Styling.ThemeVariant.Dark
                        : Avalonia.Styling.ThemeVariant.Light;
                }
            });
    }

    private class PrototypeSettingsData
    {
        public bool IsDebugMode { get; set; }
        public bool IsDarkTheme { get; set; }
        public string? SelectedWalletId { get; set; }
    }
}

/// <summary>
/// Base type for sidebar navigation entries (items and group headers).
/// </summary>
public abstract record NavEntry;

/// <summary>
/// A selectable sidebar navigation item with an icon resource key and display label.
/// Icon refers to a StreamGeometry key in Icons.axaml (e.g. "NavIconHome").
/// IconIsFilled indicates the icon uses Fill instead of Stroke rendering (e.g. the wallet icon).
/// </summary>
public record NavItem(string Label, string Icon, bool IconIsFilled = false) : NavEntry;

/// <summary>
/// A non-selectable group header with a divider line and uppercase label.
/// </summary>
public record NavGroupHeader(string Title) : NavEntry;

public partial class ShellViewModel : ReactiveObject
{
    private readonly PortfolioViewModel _portfolioVm;
    private readonly SignatureStore _signatureStore;
    private readonly Func<string, object?> _viewFactory;
    private readonly IWalletContext _walletContext;
    private readonly IInvestmentAppService _investmentAppService;
    private readonly ICurrencyService _currencyService;
    private readonly PrototypeSettings _prototypeSettings;

    [Reactive] private NavItem? selectedNavItem;
    [Reactive] private bool isSettingsOpen;
    [Reactive] private string? sectionTitleOverride;

    // ── Mobile tab bar state ──
    // Vue: mobileActiveTab, mobileInvestorSubTab, mobileFounderSubTab in App.vue
    [Reactive] private string mobileActiveTab = "home";
    [Reactive] private string mobileInvestorSubTab = "find-projects";
    [Reactive] private string mobileFounderSubTab = "my-projects";

    // ── Detail view state tracking (for mobile sub-tab/back-button visibility) ──
    // Vue: showProjectDetail, showInvestPage, showInvestmentDetail, showManageFunds, isCreatingProject
    // Sections set these when entering/exiting detail views so ShellView can react.
    [Reactive] private bool isProjectDetailOpen;
    [Reactive] private bool isInvestPageOpen;
    /// <summary>CTA verb for the mobile floating bar — "Invest", "Fund", or "Subscribe".</summary>
    [Reactive] private string projectDetailActionVerb = "Invest";
    [Reactive] private bool isInvestmentDetailOpen;
    [Reactive] private bool isManageFundsOpen;
    [Reactive] private bool isCreatingProject;

    /// <summary>
    /// Reference to the LayoutModeService singleton for XAML bindings.
    /// </summary>
    public LayoutModeService Layout => LayoutModeService.Instance;

    /// <summary>
    /// When true, the next MyProjectsView instance will auto-open the create wizard.
    /// Consumed (reset to false) by MyProjectsView on init.
    /// </summary>
    [Reactive] private bool pendingLaunchWizard;

    /// <summary>
    /// Nav label of the section that launched the create-project wizard.
    /// When the wizard closes, we route back here. Null means the user opened
    /// the wizard from within My Projects itself (no routing needed).
    /// </summary>
    public string? WizardOriginNavLabel { get; set; }

    /// <summary>
    /// Shell-level modal overlay state. Any section can push a modal view here
    /// to have it rendered above the entire app (sidebar + content).
    /// </summary>
    [Reactive] private bool isModalOpen;
    [Reactive] private object? modalContent;

    /// <summary>
    /// Toast notification message. Set by ShowToast(), auto-cleared after timeout.
    /// Vue: showCopyToast / showSaveToast + toastMessage in App.vue.
    /// </summary>
    [Reactive] private string? toastMessage;

    /// <summary>True when a toast is visible (non-null message).</summary>
    public bool HasToast => ToastMessage != null;

    /// <summary>Timer handle to cancel previous toast if a new one fires before timeout.</summary>
    private CancellationTokenSource? _toastCts;

    /// <summary>
    /// Currently selected wallet for the header wallet switcher.
    /// Delegated to IWalletContext.SelectedWallet.
    /// Vue: selectedWalletId + selectedWalletName computed in App.vue.
    /// </summary>
    public WalletInfo? SelectedWallet => _walletContext.SelectedWallet;

    /// <summary>
    /// All wallets available for switching.
    /// Delegated to IWalletContext.Wallets.
    /// Vue: filteredWalletsForModal computed from walletGroups.
    /// </summary>
    public ReadOnlyObservableCollection<WalletInfo> SwitcherWallets => _walletContext.Wallets;

    /// <summary>Display name for the header button. Shows "Select Wallet" if none selected.</summary>
    public string SelectedWalletName => SelectedWallet?.Name ?? "Select Wallet";

    /// <summary>Currency symbol from ICurrencyService (e.g. "BTC", "TBTC").</summary>
    public string CurrencySymbol => _currencyService.Symbol;

    /// <summary>Invested balance display string for the header. Updated from SDK GetTotalInvested.</summary>
    [Reactive] private string investedBalanceDisplay = "0.0000";

    /// <summary>Available balance display string for the header. Uses selected wallet balance.</summary>
    public string AvailableBalanceDisplay =>
        SelectedWallet != null
            ? SelectedWallet.FormattedBalance(_currencyService.Symbol)
            : "0.0000 " + _currencyService.Symbol;

    /// <summary>
    /// Profile name shown in the header tag. Null when running the "Default" profile (tag hidden).
    /// </summary>
    public string? ProfileName { get; }

    public ShellViewModel(PortfolioViewModel portfolioVm, SignatureStore signatureStore, Func<string, object?> viewFactory, IWalletContext walletContext, IInvestmentAppService investmentAppService, ICurrencyService currencyService, ProfileContext profileContext, PrototypeSettings prototypeSettings)
    {
        _portfolioVm = portfolioVm;
        _signatureStore = signatureStore;
        _viewFactory = viewFactory;
        _walletContext = walletContext;
        _investmentAppService = investmentAppService;
        _currencyService = currencyService;
        _prototypeSettings = prototypeSettings;
        _instance = this;

        // Mobile perf: pre-warm all tab views after first render so the first
        // tap on any tab is a cache hit. Each view is created one at a time on
        // ApplicationIdle so we don't block any user interaction.
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            PreWarmTabViews();
        }

        // Hide profile tag for the default profile, show for all others
        var profile = profileContext.ProfileName;
        ProfileName = string.Equals(profile, "Default", StringComparison.OrdinalIgnoreCase) ? null : profile;

        NavEntries = new ObservableCollection<NavEntry>
        {
            // Ungrouped
            new NavItem("Home", "NavIconHome"),
            new NavItem("Funds", "NavIconWallet", IconIsFilled: true),
            // INVESTOR group
            new NavGroupHeader("INVESTOR"),
            new NavItem("Find Projects", "NavIconSearch"),
            new NavItem("Funded", "NavIconTrendUp"),
            // FOUNDER group
            new NavGroupHeader("FOUNDER"),
            new NavItem("My Projects", "NavIconDocument"),
            new NavItem("Funders", "NavIconUsers"),
        };

        selectedNavItem = (NavItem)NavEntries[0];

        // When a sidebar item is selected, leave settings mode
        this.WhenAnyValue(x => x.SelectedNavItem)
            .Where(item => item != null)
            .Subscribe(_ =>
            {
                IsSettingsOpen = false;
                SectionTitleOverride = null;
                SyncMobileTabState();
                this.RaisePropertyChanged(nameof(CurrentSectionContent));
                this.RaisePropertyChanged(nameof(SelectedSectionName));
            });

        this.WhenAnyValue(x => x.IsSettingsOpen)
            .Where(open => open)
            .Subscribe(_ =>
            {
                SyncMobileTabState();
                this.RaisePropertyChanged(nameof(CurrentSectionContent));
                this.RaisePropertyChanged(nameof(SelectedSectionName));
            });

        this.WhenAnyValue(x => x.SectionTitleOverride)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(SelectedSectionName)));

        // ── Initialize wallet context and subscribe to updates ──
        _ = _walletContext.ReloadAsync();
        _walletContext.WalletsUpdated
            .Subscribe(unit =>
            {
                this.RaisePropertyChanged(nameof(SelectedWallet));
                this.RaisePropertyChanged(nameof(SelectedWalletName));
                this.RaisePropertyChanged(nameof(AvailableBalanceDisplay));
                this.RaisePropertyChanged(nameof(SwitcherWallets));
                _ = LoadTotalInvestedAsync(SelectedWallet);
            });

        // When toast message changes, notify HasToast
        this.WhenAnyValue(x => x.ToastMessage)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasToast)));
    }

    public ObservableCollection<NavEntry> NavEntries { get; }

    public string SelectedSectionName => IsSettingsOpen ? "Settings" : (SectionTitleOverride ?? SelectedNavItem?.Label ?? "");

    public void NavigateToSettings()
    {
        SelectedNavItem = null;
        IsSettingsOpen = true;
    }

    /// <summary>
    /// Navigate to "My Projects" and auto-open the create project wizard.
    /// Called from the Home view's "Launch a Project" button.
    /// </summary>
    public void NavigateToMyProjectsAndLaunch()
    {
        // Remember where the user came from so the wizard can route back on close.
        WizardOriginNavLabel = SelectedNavItem?.Label;
        PendingLaunchWizard = true;
        var myProjectsItem = NavEntries.OfType<NavItem>().FirstOrDefault(n => n.Label == "My Projects");
        if (myProjectsItem != null)
        {
            SelectedNavItem = myProjectsItem;
        }
    }

    /// <summary>
    /// Called when the create-project wizard is dismissed. If the wizard was
    /// opened from another section (e.g. Home), route back there; otherwise
    /// stay on My Projects.
    /// </summary>
    public void OnCreateWizardClosed()
    {
        var origin = WizardOriginNavLabel;
        WizardOriginNavLabel = null;
        if (string.IsNullOrEmpty(origin) || origin == "My Projects") return;

        // Only route back if we're currently on My Projects. If the user already
        // navigated elsewhere (e.g. clicked a different sidebar item while the
        // wizard was open), the close was triggered by onReuse resetting state —
        // we shouldn't override the navigation they just performed.
        if (SelectedNavItem?.Label != "My Projects") return;

        var target = NavEntries.OfType<NavItem>().FirstOrDefault(n => n.Label == origin);
        if (target != null)
        {
            SelectedNavItem = target;
        }
    }

    /// <summary>
    /// Navigate to "Find Projects" section.
    /// Called from the Home view's "Find Projects" button.
    /// </summary>
    public void NavigateToFindProjects()
    {
        var findProjectsItem = NavEntries.OfType<NavItem>().FirstOrDefault(n => n.Label == "Find Projects");
        if (findProjectsItem != null)
        {
            SelectedNavItem = findProjectsItem;
        }
    }

    /// <summary>
    /// Navigate to "Funded" (Portfolio) section.
    /// Called after a successful investment to show the user their funded projects.
    /// </summary>
    public void NavigateToFunded()
    {
        var fundedItem = NavEntries.OfType<NavItem>().FirstOrDefault(n => n.Label == "Funded");
        if (fundedItem != null)
        {
            SelectedNavItem = fundedItem;
        }
    }

    /// <summary>
    /// Navigate to "Funds" section.
    /// Called from the Recovery flow success modal ("Go to Funds Tab").
    /// </summary>
    public void NavigateToFunds()
    {
        var fundsItem = NavEntries.OfType<NavItem>().FirstOrDefault(n => n.Label == "Funds");
        if (fundsItem != null)
        {
            SelectedNavItem = fundsItem;
        }
    }

    /// <summary>
    /// Select a wallet from the switcher modal.
    /// Delegates to IWalletContext.SelectedWallet, which handles deselect/select/persist.
    /// Vue: selectWallet(walletId) in App.vue.
    /// </summary>
    public void SelectSwitcherWallet(WalletInfo wallet)
    {
        _walletContext.SelectedWallet = wallet;
        this.RaisePropertyChanged(nameof(SelectedWallet));
        this.RaisePropertyChanged(nameof(SelectedWalletName));
        this.RaisePropertyChanged(nameof(AvailableBalanceDisplay));
        _ = LoadTotalInvestedAsync(wallet);
    }

    /// <summary>
    /// Reload wallets from SDK via IWalletContext.
    /// </summary>
    public async Task ReloadWalletsAsync()
    {
        await _walletContext.ReloadAsync();
    }

    /// <summary>
    /// Load the total invested amount for the given wallet from the SDK.
    /// Updates the InvestedBalanceDisplay header property.
    /// </summary>
    private async Task LoadTotalInvestedAsync(WalletInfo? wallet)
    {
        if (wallet == null)
        {
            InvestedBalanceDisplay = "0.0000 " + _currencyService.Symbol;
            return;
        }

        try
        {
            var result = await _investmentAppService.GetTotalInvested(
                new Angor.Sdk.Funding.Investor.Operations.GetTotalInvested.GetTotalInvestedRequest(
                    wallet.Id));

            if (result.IsSuccess)
            {
                var btc = result.Value.TotalInvestedSats.ToUnitBtc();
                InvestedBalanceDisplay = btc.ToString("F4", CultureInfo.InvariantCulture) + " " + _currencyService.Symbol;
            }
        }
        catch (Exception ex)
        {
            var logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<ShellViewModel>();
            logger.LogWarning(ex, "LoadTotalInvestedAsync failed for wallet '{WalletId}'", wallet.Id.Value);
        }
    }

    // ── Mobile tab bar navigation ──
    // Vue: handleMobileTabChange, handleInvestorSubTabChange, handleFounderSubTabChange

    /// <summary>
    /// Handle a mobile bottom tab bar tap.
    /// Vue: handleMobileTabChange() in App.vue (line 9075).
    /// Maps the 5 mobile tabs to the sidebar nav items.
    /// </summary>
    public void HandleMobileTabChange(string tab)
    {
        // Vue special case (line 9077): clicking founder tab while ManageFunds is open
        // calls backFromManageFunds() and returns early.
        if (tab == "founder" && IsManageFundsOpen)
        {
            CloseManageFundsFromShell();
            return;
        }

        MobileActiveTab = tab;

        switch (tab)
        {
            case "home":
                SelectNavByLabel("Home");
                break;
            case "funds":
                SelectNavByLabel("Funds");
                break;
            case "investor":
                // Remember last sub-tab (Vue: changePage(mobileInvestorSubTab.value))
                SelectNavByLabel(MobileInvestorSubTab == "investments" ? "Funded" : "Find Projects");
                break;
            case "founder":
                SelectNavByLabel(MobileFounderSubTab == "funders" ? "Funders" : "My Projects");
                break;
            case "settings":
                NavigateToSettings();
                break;
        }
    }

    /// <summary>
    /// Handle investor floating sub-tab change.
    /// Vue: handleInvestorSubTabChange() in App.vue.
    /// </summary>
    public void HandleInvestorSubTabChange(string subTab)
    {
        MobileInvestorSubTab = subTab;
        MobileActiveTab = "investor";
        SelectNavByLabel(subTab == "investments" ? "Funded" : "Find Projects");
    }

    /// <summary>
    /// Handle founder floating sub-tab change.
    /// Vue: handleFounderSubTabChange() in App.vue.
    /// </summary>
    public void HandleFounderSubTabChange(string subTab)
    {
        MobileFounderSubTab = subTab;
        MobileActiveTab = "founder";
        SelectNavByLabel(subTab == "funders" ? "Funders" : "My Projects");
    }

    /// <summary>
    /// Sync the mobile tab state when desktop sidebar navigation changes.
    /// Vue: changePage() sync logic in App.vue (line 8546).
    /// </summary>
    private void SyncMobileTabState()
    {
        if (IsSettingsOpen)
        {
            MobileActiveTab = "settings";
            return;
        }

        switch (SelectedNavItem?.Label)
        {
            case "Home":
                MobileActiveTab = "home";
                break;
            case "Funds":
                MobileActiveTab = "funds";
                break;
            case "Find Projects":
                MobileActiveTab = "investor";
                MobileInvestorSubTab = "find-projects";
                break;
            case "Funded":
                MobileActiveTab = "investor";
                MobileInvestorSubTab = "investments";
                break;
            case "My Projects":
                MobileActiveTab = "founder";
                MobileFounderSubTab = "my-projects";
                break;
            case "Funders":
                MobileActiveTab = "founder";
                MobileFounderSubTab = "funders";
                break;
            default:
                // Unknown / null label — clear tab to avoid stale overlays leaking
                // (e.g. Founder sub-tabs lingering after navigating elsewhere).
                MobileActiveTab = "";
                break;
        }
    }

    /// <summary>Select a sidebar nav item by label.</summary>
    private void SelectNavByLabel(string label)
    {
        var item = NavEntries.OfType<NavItem>().FirstOrDefault(n => n.Label == label);
        if (item != null)
            SelectedNavItem = item;
    }

    // ═══════════════════════════════════════════════════════════════
    // MOBILE BACK BUTTON ACTIONS
    // Vue: backToProjects(), backToProjectDetail(), backToInvestments(),
    //      backFromManageFunds() in App.vue
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Back from ProjectDetail/InvestPage in the investor flow.
    /// Vue: showInvestPage ? backToProjectDetail() : backToProjects()
    /// </summary>
    public void BackFromInvestorDetail()
    {
        if (IsInvestPageOpen)
        {
            // Back from invest page → project detail
            if (_viewCache.TryGetValue("Find Projects", out var v) &&
                v is FindProjectsView { DataContext: FindProjectsViewModel fpVm })
            {
                fpVm.CloseInvestPage();
            }
        }
        else if (IsProjectDetailOpen)
        {
            // Back from project detail → project list
            if (_viewCache.TryGetValue("Find Projects", out var v) &&
                v is FindProjectsView { DataContext: FindProjectsViewModel fpVm })
            {
                fpVm.CloseProjectDetail();
            }
        }
    }

    /// <summary>
    /// Back from InvestmentDetail → portfolio list.
    /// Vue: backToInvestments()
    /// </summary>
    public void BackFromInvestmentDetail()
    {
        _portfolioVm.CloseInvestmentDetail();
    }

    /// <summary>
    /// Close ManageFunds from the shell (used by founder tab special case and back button).
    /// Vue: backFromManageFunds()
    /// </summary>
    public void CloseManageFundsFromShell()
    {
        if (_viewCache.TryGetValue("My Projects", out var v) &&
            v is MyProjectsView { DataContext: MyProjectsViewModel mpVm })
        {
            mpVm.CloseManageProject();
        }
        // Ensure the founder tab is set and sub-tabs re-appear
        MobileActiveTab = "founder";
        MobileFounderSubTab = "my-projects";
    }

    /// <summary>
    /// Share the currently selected investor project via the shell modal.
    /// </summary>
    public void ShareCurrentInvestorProject()
    {
        if (IsModalOpen) return;
        if (!_viewCache.TryGetValue("Find Projects", out var v) ||
            v is not FindProjectsView { DataContext: FindProjectsViewModel fpVm } ||
            fpVm.SelectedProject is not { } project)
            return;

        ShowModal(new Shared.Controls.ShareModal(project.ProjectName, project.ShortDescription));
    }

    /// <summary>
    /// Action for the invest/submit CTA button on the mobile back bar.
    /// Vue: showInvestPage ? handleInvestPageAction() : viewInvestPage()
    /// </summary>
    public void MobileInvestAction()
    {
        if (IsInvestPageOpen)
        {
            // Trigger invest submit via the FindProjectsViewModel's InvestPageViewModel
            if (_viewCache.TryGetValue("Find Projects", out var iv) &&
                iv is FindProjectsView { DataContext: FindProjectsViewModel fpVm2 } &&
                fpVm2.InvestPageViewModel is { } investVm)
            {
                if (investVm.CanSubmit)
                    investVm.Submit();
            }
        }
        else if (IsProjectDetailOpen)
        {
            // Open invest page from project detail
            if (_viewCache.TryGetValue("Find Projects", out var v) &&
                v is FindProjectsView { DataContext: FindProjectsViewModel fpVm })
            {
                fpVm.OpenInvestPage();
            }
        }
    }

    /// <summary>
    /// Reset all detail view state flags. Called on breakpoint crossing
    /// or when navigating to a different section to ensure clean state.
    /// Vue: changePage() resets showProjectDetail, showInvestPage, selectedProject.
    /// </summary>
    public void ResetDetailViewState()
    {
        IsProjectDetailOpen = false;
        IsInvestPageOpen = false;
        IsInvestmentDetailOpen = false;
        IsManageFundsOpen = false;
        IsCreatingProject = false;
    }

    public void ResetAfterDataWipe()
    {
        _signatureStore.Clear();
        _portfolioVm.ResetAfterDataWipe();
        InvestedBalanceDisplay = "0.0000 " + _currencyService.Symbol;
        ToastMessage = null;
        ModalContent = null;
        IsModalOpen = false;
        ClearViewCache();
        this.RaisePropertyChanged(nameof(SelectedWallet));
        this.RaisePropertyChanged(nameof(AvailableBalanceDisplay));
        this.RaisePropertyChanged(nameof(SelectedWalletName));
        this.RaisePropertyChanged(nameof(SwitcherWallets));
    }

    /// <summary>
    /// Show a toast notification with auto-dismiss.
    /// Vue: showCopyToast = true → setTimeout → showCopyToast = false.
    /// If called while a toast is already visible, cancels the previous timeout
    /// and restarts with the new message.
    /// </summary>
    /// <param name="message">Toast text (e.g. "Copied to clipboard").</param>
    /// <param name="durationMs">Auto-dismiss delay. If 0 or not specified, auto-scales based on message length
    /// (min 3s, +1s per 30 chars, max 10s). Vue: 2000-3000ms for copy, 5000ms for save.</param>
    public void ShowToast(string message, int durationMs = 0)
    {
        // Cancel any previous dismiss timer
        _toastCts?.Cancel();
        _toastCts = new CancellationTokenSource();
        var token = _toastCts.Token;

        ToastMessage = message;

        // Auto-scale duration based on message length if not explicitly specified
        if (durationMs <= 0)
        {
            var charBasedMs = 3000 + (message.Length / 30) * 1000;
            durationMs = Math.Clamp(charBasedMs, 3000, 10000);
        }

        // Auto-dismiss after duration
        _ = DismissToastAsync(durationMs, token);
    }

    private async Task DismissToastAsync(int durationMs, CancellationToken token)
    {
        try
        {
            await Task.Delay(durationMs, token);
            if (!token.IsCancellationRequested)
                ToastMessage = null;
        }
        catch (TaskCanceledException)
        {
            // Expected when a new toast replaces the current one
        }
    }

    /// <summary>
    /// Show a modal overlay above the entire app window.
    /// The content control will be displayed centered over a backdrop scrim.
    /// </summary>
    public void ShowModal(object content)
    {
        ModalContent = content;
        IsModalOpen = true;
    }

    /// <summary>
    /// Close the shell-level modal overlay.
    /// </summary>
    public void HideModal()
    {
        IsModalOpen = false;
        ModalContent = null;
    }

    public bool IsDarkThemeEnabled
    {
        get => _prototypeSettings.IsDarkTheme;
        set
        {
            _prototypeSettings.IsDarkTheme = value;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Cached section views — avoids recreating views (and their subscriptions,
    /// image loaders, sample data) on every sidebar navigation.
    /// Key = NavItem label (or "Settings"); Value = the View UserControl.
    /// </summary>
    private readonly Dictionary<string, object> _viewCache = new();

    /// <summary>Clear the view cache so sections are recreated with fresh data on next navigation.</summary>
    public void ClearViewCache() => _viewCache.Clear();

    /// <summary>
    /// Ensure a view exists in the cache for the given key. Creates it via the
    /// view factory if missing. Used by SectionPanel on mobile for on-demand
    /// creation when pre-warm hasn't reached the view yet.
    /// </summary>
    public void EnsureViewCreated(string key)
    {
        if (_viewCache.ContainsKey(key)) return;
        var sw = Stopwatch.StartNew();
        var view = _viewFactory(key);
        sw.Stop();
        if (view != null)
        {
            _viewCache[key] = view;
            ViewPreWarmed?.Invoke(key, view);
            AttachRenderTiming(key, view, sw.ElapsedMilliseconds);
        }
        PerfLog("EnsureViewCreated", $"key={key} factoryMs={sw.ElapsedMilliseconds}");
    }

    /// <summary>Read-only access to the view cache for SectionPanel population.</summary>
    public IReadOnlyDictionary<string, object> ViewCache => _viewCache;

    /// <summary>
    /// Raised after each view is pre-warmed, with (key, view). ShellView uses
    /// this to incrementally add views to the SectionPanel on mobile.
    /// </summary>
    public event Action<string, object>? ViewPreWarmed;

    /// <summary>
    /// Resolves the current section key from SelectedNavItem / IsSettingsOpen.
    /// Used by ShellView to drive SectionPanel on mobile without duplicating logic.
    /// </summary>
    public string? CurrentSectionKey
    {
        get
        {
            if (IsSettingsOpen) return "Settings";
            return SelectedNavItem?.Label;
        }
    }

    public object? CurrentSectionContent
    {
        get
        {
            // On mobile, SectionPanel manages views via IsVisible toggling.
            // Returning views here would cause Avalonia to parent them to the
            // ContentControl AND the SectionPanel simultaneously → SIGABRT.
            if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
                return null;
            var swGetter = Stopwatch.StartNew();
            object? result;
            if (IsSettingsOpen)
            {
                result = GetOrCreateView("Settings");
            }
            else
            {
                result = SelectedNavItem?.Label switch
                {
                    // Home: re-apply responsive layout on tab return. Avalonia's layout
                    // engine retains stale measure caches on the HomeGrid columns/rows
                    // when returning via the desktop ContentControl swap, so star cols
                    // can keep a stale width that doesn't match the new available size
                    // (visible as the Home page not scaling after resizing the window
                    // while on another tab and coming back).
                    "Home" => GetOrCreateView("Home",
                        onReuse: v =>
                        {
                            if (v is Sections.Home.HomeView homeView)
                                homeView.OnBecameActive();
                        }),
                    "Funds" => GetOrCreateView("Funds"),
                    "Find Projects" => GetOrCreateView("Find Projects",
                        onReuse: v =>
                        {
                            // Reset sub-nav state when re-selecting Find Projects from sidebar
                            if (v is FindProjectsView { DataContext: FindProjectsViewModel fpVm })
                            {
                                fpVm.CloseInvestPage();
                                fpVm.CloseProjectDetail();
                            }
                        }),
                    "Funded" => GetOrCreateView("Funded",
                        onReuse: _ => _portfolioVm.CloseInvestmentDetail()),
                    "My Projects" => GetOrCreateView("My Projects",
                        onReuse: v =>
                        {
                            // Reset sub-nav state when re-selecting My Projects from sidebar.
                            // Skip when a wizard-launch is pending (e.g. Home → Launch a Project
                            // while the previous wizard was never explicitly closed) — otherwise
                            // we'd close the wizard we're about to open and route back to the origin.
                            if (v is MyProjectsView { DataContext: MyProjectsViewModel mpVm })
                            {
                                if (!PendingLaunchWizard)
                                    mpVm.CloseCreateWizard();
                                mpVm.CloseManageProject();
                            }
                        }),
                    "Funders" => GetOrCreateView("Funders"),
                    _ => null,
                };
            }
            swGetter.Stop();
            PerfLog("CurrentSectionContent", $"label={SelectedNavItem?.Label} getterMs={swGetter.ElapsedMilliseconds}");
            return result;
        }
    }

    /// <summary>
    /// Pre-create all tab views one-by-one on ApplicationIdle dispatches so
    /// the first user tap on any tab is a cache hit. Each dispatch creates
    /// one view, allowing the render pipeline to paint between inflates.
    /// Only runs on mobile where XAML inflate cost is high.
    /// </summary>
    private void PreWarmTabViews()
    {
        var tabKeys = new[] { "Find Projects", "My Projects", "Funds", "Settings", "Funded", "Funders" };
        var index = 0;

        void WarmNext()
        {
            if (index >= tabKeys.Length)
            {
                // After tabs are warm, pre-inflate drill-down views so first drill
                // is a DataContext swap rather than a ~350ms XAML inflate.
                Avalonia.Threading.Dispatcher.UIThread.Post(
                    PreWarmDrillDownViews,
                    Avalonia.Threading.DispatcherPriority.ApplicationIdle);
                return;
            }
            var key = tabKeys[index++];
            if (_viewCache.ContainsKey(key))
            {
                // Already cached, skip to next
                Avalonia.Threading.Dispatcher.UIThread.Post(WarmNext, Avalonia.Threading.DispatcherPriority.ApplicationIdle);
                return;
            }
            var sw = Stopwatch.StartNew();
            var view = _viewFactory(key);
            sw.Stop();
            if (view != null)
            {
                _viewCache[key] = view;
                ViewPreWarmed?.Invoke(key, view);
            }
            PerfLog("PreWarm", $"key={key} factoryMs={sw.ElapsedMilliseconds}");
            // Schedule next view on next idle frame
            Avalonia.Threading.Dispatcher.UIThread.Post(WarmNext, Avalonia.Threading.DispatcherPriority.ApplicationIdle);
        }

        // Delay the first pre-warm slightly so first-paint of Home completes
        Avalonia.Threading.Dispatcher.UIThread.Post(WarmNext, Avalonia.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// After tab views are warm, pre-inflate the investor drill-down views
    /// (ProjectDetailView + InvestPageView) inside FindProjectsView. Mobile only.
    /// Also primes ManageProjectView inside MyProjectsView for fast founder drill-down.
    /// </summary>
    private void PreWarmDrillDownViews()
    {
        if (_viewCache.TryGetValue("Find Projects", out var view)
            && view is global::App.UI.Sections.FindProjects.FindProjectsView fpView)
        {
            fpView.PreWarmDrillDownViews();
        }

        if (_viewCache.TryGetValue("My Projects", out var mpView)
            && mpView is global::App.UI.Sections.MyProjects.MyProjectsView myProjectsView)
        {
            myProjectsView.PreWarmManageView();
        }
    }

    /// <summary>
    /// Returns a cached view or creates one via the injected view factory.
    /// Optional <paramref name="onReuse"/> callback runs when returning an already-cached view
    /// (e.g. to reset sub-navigation state).
    /// </summary>
    private object? GetOrCreateView(string key, Action<object>? onReuse = null)
    {
        if (_viewCache.TryGetValue(key, out var existing))
        {
            var swReuse = Stopwatch.StartNew();
            onReuse?.Invoke(existing);
            swReuse.Stop();
            PerfLog("GetOrCreateView", $"key={key} cached=true onReuseMs={swReuse.ElapsedMilliseconds}");
            return existing;
        }

        var sw = Stopwatch.StartNew();
        var view = _viewFactory(key);
        sw.Stop();
        PerfLog("GetOrCreateView", $"key={key} cached=false factoryMs={sw.ElapsedMilliseconds}");

        if (view != null)
        {
            _viewCache[key] = view;
            ViewPreWarmed?.Invoke(key, view);
            AttachRenderTiming(key, view, sw.ElapsedMilliseconds);
        }
        return view;
    }

    // ── Perf: programmatic tab switch for adb-driven benchmarks ──
    private static ShellViewModel? _instance;

    /// <summary>
    /// Called from Android BroadcastReceiver to switch tabs via adb.
    /// Usage: adb shell am broadcast -a io.angor.app.PERF_TAB --es tab "Investor"
    /// </summary>
    public static void SwitchTabForPerf(string tabName)
    {
        PerfLog("SwitchTabForPerf", $"ENTER tab={tabName} hasInstance={_instance != null}");
        if (_instance == null) return;
        var vm = _instance;
        var sw = Stopwatch.StartNew();

        // ── Drill-down perf commands ─────────────────────────────────
        // These simulate user taps on list items so we can benchmark
        // child view render times without needing touch input.
        if (tabName == "OpenFirstProject")
        {
            // Open the first project in Find Projects
            OpenFirstProjectForPerf();
            sw.Stop();
            PerfLog("SwitchTabForPerf", $"tab=OpenFirstProject switchMs={sw.ElapsedMilliseconds}");
            return;
        }
        if (tabName == "CloseProject")
        {
            CloseProjectForPerf();
            sw.Stop();
            PerfLog("SwitchTabForPerf", $"tab=CloseProject switchMs={sw.ElapsedMilliseconds}");
            return;
        }
        if (tabName == "LoadMore")
        {
            LoadMoreForPerf();
            sw.Stop();
            PerfLog("SwitchTabForPerf", $"tab=LoadMore switchMs={sw.ElapsedMilliseconds}");
            return;
        }
        if (tabName == "OpenInvestPage")
        {
            // Requires project already open
            OpenInvestPageForPerf();
            sw.Stop();
            PerfLog("SwitchTabForPerf", $"tab=OpenInvestPage switchMs={sw.ElapsedMilliseconds}");
            return;
        }
        if (tabName == "OpenCreateWizard")
        {
            OpenCreateWizardForPerf();
            sw.Stop();
            PerfLog("SwitchTabForPerf", $"tab=OpenCreateWizard switchMs={sw.ElapsedMilliseconds}");
            return;
        }
        if (tabName == "OpenFounderProject")
        {
            OpenFounderProjectForPerf();
            sw.Stop();
            PerfLog("SwitchTabForPerf", $"tab=OpenFounderProject switchMs={sw.ElapsedMilliseconds}");
            return;
        }
        if (tabName == "CloseFounderProject")
        {
            CloseFounderProjectForPerf();
            sw.Stop();
            PerfLog("SwitchTabForPerf", $"tab=CloseFounderProject switchMs={sw.ElapsedMilliseconds}");
            return;
        }

        // Map mobile/perf tab names to actual nav labels
        if (tabName == "Investor" || tabName == "FindProjects") tabName = "Find Projects";
        else if (tabName == "Founder" || tabName == "MyProjects") tabName = "My Projects";
        else if (tabName == "Portfolio") tabName = "Funded";

        if (tabName == "Settings")
        {
            vm.IsSettingsOpen = true;
            vm.RaisePropertyChanged(nameof(CurrentSectionContent));
            sw.Stop();
            PerfLog("SwitchTabForPerf", $"tab=Settings switchMs={sw.ElapsedMilliseconds}");
            return;
        }

        vm.IsSettingsOpen = false;
        var navItem = vm.NavEntries.OfType<NavItem>().FirstOrDefault(n => n.Label == tabName);
        if (navItem != null)
        {
            var swSet = Stopwatch.StartNew();
            vm.SelectedNavItem = navItem;
            swSet.Stop();
            PerfLog("SwitchTabForPerf", $"tab={tabName} setNavItemMs={swSet.ElapsedMilliseconds}");
            var swRaise = Stopwatch.StartNew();
            // Force re-evaluation of CurrentSectionContent
            vm.RaisePropertyChanged(nameof(CurrentSectionContent));
            swRaise.Stop();
            sw.Stop();
            PerfLog("SwitchTabForPerf", $"tab={tabName} raiseMs={swRaise.ElapsedMilliseconds} switchMs={sw.ElapsedMilliseconds}");
        }
        else
        {
            sw.Stop();
            PerfLog("SwitchTabForPerf", $"tab={tabName} NOT_FOUND switchMs={sw.ElapsedMilliseconds}");
        }
    }

    // ── Perf instrumentation (tab-switch cost attribution) ──
    // Temporary. Remove once Investor + Settings perf is characterised.
    private static ILogger? _perfLogger;
    private static ILogger PerfLogger =>
        _perfLogger ??= App.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ShellPerf");

    private static void PerfLog(string stage, string data)
    {
        PerfLogger.LogInformation("[{Stage}] {Data}", stage, data);
        // Debug.WriteLine is piped to adb logcat on .NET for Android (tag "DOTNET")
        // and to the debug output window on desktop.
        Debug.WriteLine($"[ShellPerf][{stage}] {data}");
    }

    // ── Perf drill-down helpers (broadcast-driven) ──
    // These let adb-driven perf tests open/close drill-down views without touch input.

    private static void OpenFirstProjectForPerf()
    {
        if (_instance == null) return;
        // Ensure Find Projects tab is selected first
        var navItem = _instance.NavEntries.OfType<NavItem>()
            .FirstOrDefault(n => n.Label == "Find Projects");
        if (navItem != null) _instance.SelectedNavItem = navItem;

        // Grab the Find Projects VM via the cached view
        if (_instance._viewCache.TryGetValue("Find Projects", out var view)
            && view is global::App.UI.Sections.FindProjects.FindProjectsView { DataContext: global::App.UI.Sections.FindProjects.FindProjectsViewModel fpVm })
        {
            var first = fpVm.Projects.FirstOrDefault();
            if (first != null)
            {
                PerfLog("OpenFirstProject", $"name={first.ProjectName} id={first.ProjectId}");
                fpVm.OpenProjectDetail(first);
            }
            else
            {
                PerfLog("OpenFirstProject", "NO_PROJECTS");
            }
        }
        else
        {
            PerfLog("OpenFirstProject", "FindProjectsView_NOT_CACHED");
        }
    }

    private static void CloseProjectForPerf()
    {
        if (_instance == null) return;
        if (_instance._viewCache.TryGetValue("Find Projects", out var view)
            && view is global::App.UI.Sections.FindProjects.FindProjectsView { DataContext: global::App.UI.Sections.FindProjects.FindProjectsViewModel fpVm })
        {
            fpVm.CloseInvestPage();
            fpVm.CloseProjectDetail();
        }
    }

    private static void LoadMoreForPerf()
    {
        if (_instance == null) return;
        if (_instance._viewCache.TryGetValue("Find Projects", out var view)
            && view is global::App.UI.Sections.FindProjects.FindProjectsView { DataContext: global::App.UI.Sections.FindProjects.FindProjectsViewModel fpVm })
        {
            fpVm.LoadMore();
        }
    }

    private static void OpenInvestPageForPerf()
    {
        if (_instance == null) return;
        if (_instance._viewCache.TryGetValue("Find Projects", out var view)
            && view is global::App.UI.Sections.FindProjects.FindProjectsView { DataContext: global::App.UI.Sections.FindProjects.FindProjectsViewModel fpVm })
        {
            if (fpVm.SelectedProject == null)
            {
                var first = fpVm.Projects.FirstOrDefault();
                if (first != null) fpVm.OpenProjectDetail(first);
            }
            fpVm.OpenInvestPage();
        }
    }

    private static void OpenCreateWizardForPerf()
    {
        if (_instance == null) return;
        // Ensure My Projects tab is selected
        var navItem = _instance.NavEntries.OfType<NavItem>()
            .FirstOrDefault(n => n.Label == "My Projects");
        if (navItem != null) _instance.SelectedNavItem = navItem;

        if (_instance._viewCache.TryGetValue("My Projects", out var view)
            && view is global::App.UI.Sections.MyProjects.MyProjectsView { DataContext: global::App.UI.Sections.MyProjects.MyProjectsViewModel mpVm })
        {
            // Match the real user flow: reset wizard before opening
            mpVm.CreateProjectVm.ResetWizard();
            mpVm.LaunchCreateWizard();
        }
    }

    private static void OpenFounderProjectForPerf()
    {
        if (_instance == null) return;
        // Ensure My Projects tab is selected first
        var navItem = _instance.NavEntries.OfType<NavItem>()
            .FirstOrDefault(n => n.Label == "My Projects");
        if (navItem != null) _instance.SelectedNavItem = navItem;

        if (_instance._viewCache.TryGetValue("My Projects", out var view)
            && view is global::App.UI.Sections.MyProjects.MyProjectsView { DataContext: global::App.UI.Sections.MyProjects.MyProjectsViewModel mpVm })
        {
            var first = mpVm.Projects.FirstOrDefault();
            if (first != null)
            {
                PerfLog("OpenFounderProject", $"name={first.Name} id={first.ProjectIdentifier}");
                mpVm.OpenManageProject(first);
            }
            else
            {
                PerfLog("OpenFounderProject", "NO_PROJECTS");
            }
        }
        else
        {
            PerfLog("OpenFounderProject", "MyProjectsView_NOT_CACHED");
        }
    }

    private static void CloseFounderProjectForPerf()
    {
        if (_instance == null) return;
        if (_instance._viewCache.TryGetValue("My Projects", out var view)
            && view is global::App.UI.Sections.MyProjects.MyProjectsView { DataContext: global::App.UI.Sections.MyProjects.MyProjectsViewModel mpVm })
        {
            mpVm.CloseManageProject();
        }
    }

    private static void AttachRenderTiming(string key, object view, long factoryMs)
    {
        if (view is not Avalonia.Controls.Control control) return;

        var attachSw = Stopwatch.StartNew();
        void OnAttached(object? _, Avalonia.VisualTreeAttachmentEventArgs __)
        {
            attachSw.Stop();
            control.AttachedToVisualTree -= OnAttached;

            var renderSw = Stopwatch.StartNew();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                renderSw.Stop();
                PerfLog("FirstRender",
                    $"key={key} factoryMs={factoryMs} attachMs={attachSw.ElapsedMilliseconds} firstRenderIdleMs={renderSw.ElapsedMilliseconds} totalMs={factoryMs + attachSw.ElapsedMilliseconds + renderSw.ElapsedMilliseconds}");
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
        control.AttachedToVisualTree += OnAttached;
    }
}
