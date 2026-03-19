using System.Collections.ObjectModel;
using System.Globalization;
using Angor.Sdk.Wallet.Application;
using System.Threading;
using Avalonia2.UI.Sections.FindProjects;
using Avalonia2.UI.Sections.MyProjects;
using Avalonia2.UI.Sections.Portfolio;
using Avalonia2.UI.Shared;

namespace Avalonia2.UI.Shell;

/// <summary>
/// A wallet item for the header wallet-switcher modal.
/// Vue: walletGroups[].wallets[] in App.vue — each wallet has id, name, type, balance, label, network.
/// Reuses the same CSS-class selection pattern as deploy/invest wallet selectors (Rule #9 compliant).
/// </summary>
public partial class WalletSwitcherItem : ReactiveObject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Type: "bitcoin", "lightning", or "liquid".</summary>
    public string WalletType { get; set; } = "bitcoin";
    public double Balance { get; set; }
    /// <summary>Subtitle: "{TypeLabel} • {SeedGroupName}".</summary>
    public string Subtitle { get; set; } = "";

    [Reactive] private bool isSelected;

    public string FormattedBalance => Balance.ToString("F4", CultureInfo.InvariantCulture) + " BTC";
}

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
/// Approval threshold (Vue: 0.01 BTC in handleInvestment):
///   - Amount &lt; 0.01 BTC → auto-approved (status='approved'), investment immediately active
///   - Amount >= 0.01 BTC → status='waiting', requires founder manual approval
/// </summary>
public class SignatureStore
{
    private int _nextId = 100;

    /// <summary>All signatures across all projects.</summary>
    public ObservableCollection<SharedSignature> AllSignatures { get; } = new();

    /// <summary>Event raised when a signature's status changes (approve/reject).</summary>
    public event Action<SharedSignature>? SignatureStatusChanged;

    /// <summary>
    /// Add a new signature for an investment.
    /// Vue: handleInvestment() in App.vue (line 8806).
    /// Applies auto-approval threshold: &lt; 0.01 BTC = auto-approved.
    /// </summary>
    public SharedSignature AddSignature(string projectId, string projectTitle, string amount)
    {
        var amountValue = double.TryParse(amount, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var a) ? a : 0;

        // Vue threshold: investments < 0.01 BTC are auto-approved
        var requiresApproval = amountValue >= Constants.AutoApprovalThreshold;
        var now = DateTime.Now;

        var sig = new SharedSignature
        {
            Id = _nextId++,
            ProjectId = projectId,
            ProjectTitle = projectTitle,
            Amount = amount,
            Currency = "BTC",
            Date = now.ToString("MMM dd, yyyy"),
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
    /// <summary>
    /// When true, sections show hardcoded sample data (populated state).
    /// When false, sections show empty states.
    /// Default = true so the app starts populated for demos.
    /// </summary>
    [Reactive] private bool showPopulatedApp = true;
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
    private readonly Func<string, object?> _viewFactory;
    private readonly IWalletAppService _walletAppService;

    [Reactive] private NavItem? selectedNavItem;
    [Reactive] private bool isSettingsOpen;
    [Reactive] private string? sectionTitleOverride;

    /// <summary>
    /// When true, the next MyProjectsView instance will auto-open the create wizard.
    /// Consumed (reset to false) by MyProjectsView on init.
    /// </summary>
    [Reactive] private bool pendingLaunchWizard;

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
    /// Vue: selectedWalletId + selectedWalletName computed in App.vue.
    /// </summary>
    [Reactive] private WalletSwitcherItem? selectedWallet;

    /// <summary>
    /// All wallets available for switching (mainnet).
    /// Vue: filteredWalletsForModal computed from walletGroups.
    /// </summary>
    public ObservableCollection<WalletSwitcherItem> SwitcherWallets { get; } = new();

    /// <summary>Display name for the header button. Shows "Select Wallet" if none selected.</summary>
    public string SelectedWalletName => SelectedWallet?.Name ?? "Select Wallet";

    /// <summary>Invested balance display string for the header.</summary>
    public string InvestedBalanceDisplay => "0.0000 BTC";

    /// <summary>Available balance display string for the header. Uses selected wallet balance.</summary>
    public string AvailableBalanceDisplay =>
        SelectedWallet != null
            ? SelectedWallet.Balance.ToString("F4", CultureInfo.InvariantCulture) + " BTC"
            : "10.0000 BTC";

    // ── Mobile tab bar state ──
    // Vue: mobileActiveTab, mobileInvestorSubTab, mobileFounderSubTab in App.vue
    [Reactive] private string mobileActiveTab = "home";
    [Reactive] private string mobileInvestorSubTab = "find-projects";
    [Reactive] private string mobileFounderSubTab = "my-projects";

    /// <summary>
    /// Reference to the LayoutModeService singleton for XAML bindings.
    /// </summary>
    public LayoutModeService Layout => LayoutModeService.Instance;

    public ShellViewModel(PortfolioViewModel portfolioVm, Func<string, object?> viewFactory, IWalletAppService walletAppService)
    {
        _portfolioVm = portfolioVm;
        _viewFactory = viewFactory;
        _walletAppService = walletAppService;
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

        // ── Initialize wallet switcher data from SDK ──
        _ = LoadSwitcherWalletsAsync();

        // When selected wallet changes, update header display properties
        this.WhenAnyValue(x => x.SelectedWallet)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(SelectedWalletName));
                this.RaisePropertyChanged(nameof(AvailableBalanceDisplay));
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
        PendingLaunchWizard = true;
        var myProjectsItem = NavEntries.OfType<NavItem>().FirstOrDefault(n => n.Label == "My Projects");
        if (myProjectsItem != null)
        {
            SelectedNavItem = myProjectsItem;
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

    // ── Mobile tab bar navigation ──
    // Vue: handleMobileTabChange, handleInvestorSubTabChange, handleFounderSubTabChange

    /// <summary>
    /// Handle a mobile bottom tab bar tap.
    /// Vue: handleMobileTabChange() in App.vue (line 9075).
    /// Maps the 5 mobile tabs to the sidebar nav items.
    /// </summary>
    public void HandleMobileTabChange(string tab)
    {
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
        }
    }

    /// <summary>Select a sidebar nav item by label.</summary>
    private void SelectNavByLabel(string label)
    {
        var item = NavEntries.OfType<NavItem>().FirstOrDefault(n => n.Label == label);
        if (item != null)
            SelectedNavItem = item;
    }

    /// <summary>
    /// Select a wallet from the switcher modal.
    /// Deselects previous, selects new, updates header display.
    /// Vue: selectWallet(walletId) in App.vue.
    /// </summary>
    public void SelectSwitcherWallet(WalletSwitcherItem wallet)
    {
        // Deselect all
        foreach (var w in SwitcherWallets)
            w.IsSelected = false;

        wallet.IsSelected = true;
        SelectedWallet = wallet;
    }

    /// <summary>
    /// Load wallets from SDK for the wallet switcher.
    /// Replaces hardcoded wallet data with real SDK wallet metadatas and balances.
    /// </summary>
    private async Task LoadSwitcherWalletsAsync()
    {
        try
        {
            var metadatasResult = await _walletAppService.GetMetadatas();
            if (metadatasResult.IsFailure) return;

            SwitcherWallets.Clear();
            foreach (var meta in metadatasResult.Value)
            {
                var balanceResult = await _walletAppService.GetBalance(meta.Id);
                var balanceBtc = balanceResult.IsSuccess ? balanceResult.Value.Sats / 100_000_000.0 : 0;

                SwitcherWallets.Add(new WalletSwitcherItem
                {
                    Id = meta.Id.Value,
                    Name = meta.Name,
                    WalletType = "bitcoin",
                    Balance = balanceBtc,
                    Subtitle = "Bitcoin • Angor Account"
                });
            }

            if (SwitcherWallets.Count > 0)
                SelectSwitcherWallet(SwitcherWallets[0]);
        }
        catch
        {
            // Wallet loading failed — switcher stays empty
        }
    }

    /// <summary>
    /// Show a toast notification with auto-dismiss.
    /// Vue: showCopyToast = true → setTimeout → showCopyToast = false.
    /// If called while a toast is already visible, cancels the previous timeout
    /// and restarts with the new message.
    /// </summary>
    /// <param name="message">Toast text (e.g. "Copied to clipboard").</param>
    /// <param name="durationMs">Auto-dismiss delay. Vue: 2000-3000ms for copy, 5000ms for save.</param>
    public void ShowToast(string message, int durationMs = 2000)
    {
        // Cancel any previous dismiss timer
        _toastCts?.Cancel();
        _toastCts = new CancellationTokenSource();
        var token = _toastCts.Token;

        ToastMessage = message;

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
        get => Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        set
        {
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = value
                    ? Avalonia.Styling.ThemeVariant.Dark
                    : Avalonia.Styling.ThemeVariant.Light;
            }
            this.RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Cached section views — avoids recreating views (and their subscriptions,
    /// image loaders, sample data) on every sidebar navigation.
    /// Key = NavItem label (or "Settings"); Value = the View UserControl.
    /// </summary>
    private readonly Dictionary<string, object> _viewCache = new();

    public object? CurrentSectionContent
    {
        get
        {
            if (IsSettingsOpen)
                return GetOrCreateView("Settings");

            return SelectedNavItem?.Label switch
            {
                "Home" => GetOrCreateView("Home"),
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
                        // Reset sub-nav state when re-selecting My Projects from sidebar
                        if (v is MyProjectsView { DataContext: MyProjectsViewModel mpVm })
                        {
                            mpVm.CloseCreateWizard();
                            mpVm.CloseManageProject();
                        }
                    }),
                "Funders" => GetOrCreateView("Funders"),
                _ => null,
            };
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
            onReuse?.Invoke(existing);
            return existing;
        }

        var view = _viewFactory(key);
        if (view != null)
            _viewCache[key] = view;
        return view;
    }
}
