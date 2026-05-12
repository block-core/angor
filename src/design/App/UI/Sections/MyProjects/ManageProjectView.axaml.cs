using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using App.UI.Shared;
using App.UI.Shared.Controls;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace App.UI.Sections.MyProjects;

public partial class ManageProjectView : UserControl
{
    private ManageProjectViewModel? Vm => DataContext as ManageProjectViewModel;
    private IDisposable? _layoutSubscription;
    private readonly ILogger<ManageProjectView> _logger;

    // Cached responsive controls
    private DockPanel? _navBar;
    private Panel? _navSpacer;
    private StackPanel? _contentStack;
    private StackPanel? _mobileNavBar;

    public ManageProjectView()
    {
        InitializeComponent();
        _logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<ManageProjectView>();

        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            Classes.Add("Mobile");

        // Cache responsive controls
        _navBar = this.FindControl<DockPanel>("ManageNavBar");
        _navSpacer = this.FindControl<Panel>("ManageNavSpacer");
        _contentStack = this.FindControl<StackPanel>("ManageContentStack");
        _mobileNavBar = this.FindControl<StackPanel>("MobileNavBar");

        // ── Wire content -> modals bridge: stage buttons open claim/spent modals ──
        var contentView = this.FindControl<ManageProjectContentView>("ContentView");
        if (contentView != null)
        {
            contentView.StageButtonClicked += (stageIndex, mode) =>
            {
                var modalsView = this.FindControl<Modals.ManageProjectModalsView>("ModalsView");
                if (modalsView == null) return;

                if (mode == "Claim")
                    modalsView.OpenClaimModal(stageIndex);
                else if (mode == "Spent")
                    modalsView.OpenSpentModal(stageIndex);
            };

            contentView.EditProjectRequested += () => OnEditProjectRequested?.Invoke();
        }

        // ── Share button ──
        var shareBtn = this.FindControl<Button>("ShareButton");
        if (shareBtn != null) shareBtn.Click += OnShareClick;

        // ── Refresh button ──
        var refreshBtn = this.FindControl<Button>("RefreshButton");
        if (refreshBtn != null) refreshBtn.Click += OnRefreshClick;

        // ── Release Funds to Investors button ──
        var releaseFundsBtn = this.FindControl<Button>("ReleaseFundsNavButton");
        if (releaseFundsBtn != null) releaseFundsBtn.Click += (_, _) => OpenReleaseFundsModal();

        // ── View Private Keys button (opens shell modal password step) ──
        var viewPKBtn = this.FindControl<Button>("ViewPrivateKeysButton");
        if (viewPKBtn != null) viewPKBtn.Click += OnViewPrivateKeysClick;

        // ── Mobile nav buttons (compact only) ──
        var mobileRefresh = this.FindControl<Button>("MobileRefreshButton");
        if (mobileRefresh != null) mobileRefresh.Click += OnRefreshClick;
        var mobileViewPK = this.FindControl<Button>("MobileViewPrivateKeysButton");
        if (mobileViewPK != null) mobileViewPK.Click += OnViewPrivateKeysClick;

        // Subscribe to layout mode changes
        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);
    }

    /// <summary>
    /// Responsive layout: compact → hide nav bar (bottom tab bar provides navigation),
    /// reduce side margins and gap.
    /// Vue: <=768px → nav hidden, content padding 16px sides + 16px gap + 96px bottom.
    /// </summary>
    private void ApplyResponsiveLayout(bool isCompact)
    {
        // Hide desktop nav bar on compact — the shell's bottom tab bar + inline mobile nav provide navigation
        // Vue: .sticky-nav-bar { display: none !important; } at <=768px
        if (_navBar != null) _navBar.IsVisible = !isCompact;

        // Show inline mobile nav (Refresh + View Private Keys) on compact only
        // Vue: .sticky-nav-bar-mobile — md:hidden
        if (_mobileNavBar != null) _mobileNavBar.IsVisible = isCompact;

        // Adjust spacer: on compact use 16px top (no floating nav); on desktop use 92px
        if (_navSpacer != null) _navSpacer.Height = isCompact ? 16 : 92;

        // Adjust content margins and spacing.
        // Vue: <=768px → .content-grid { gap: 16px; padding: 0 16px 96px 16px; }
        // 96px bottom on compact clears the shell-level fixed "Back to My Projects" green bar
        // (52px button at bottom-20 → ~20+52+24 safe gap).
        if (_contentStack != null)
        {
            _contentStack.Spacing = isCompact ? 16 : 24;
            _contentStack.Margin = isCompact
                ? new Avalonia.Thickness(16, 0, 16, 96)
                : new Avalonia.Thickness(24, 0, 24, 24);
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    // Track the back button handler to prevent accumulation across SetBackAction calls
    private Button? _backBtn;
    private EventHandler<RoutedEventArgs>? _backClickHandler;

    /// <summary>
    /// Raised when the user clicks "Edit Project" in the content view.
    /// The parent (MyProjectsView) wires this to open the edit profile panel.
    /// </summary>
    public Action? OnEditProjectRequested { get; set; }

    /// <summary>
    /// Wire the Back button to navigate back to the project list.
    /// Called from MyProjectsView code-behind after the view is created.
    /// Removes any previous handler before adding the new one.
    /// </summary>
    public void SetBackAction(Action backAction)
    {
        _backBtn ??= this.FindControl<Button>("BackButton");
        if (_backBtn == null) return;

        // Remove previous handler to prevent accumulation
        if (_backClickHandler != null)
            _backBtn.Click -= _backClickHandler;

        _backClickHandler = (_, _) => backAction();
        _backBtn.Click += _backClickHandler;
    }

    /// <summary>
    /// Opens the Release Funds modal. Call this from an external "Release Funds Back to Investors" button.
    /// </summary>
    public void OpenReleaseFundsModal()
    {
        var modalsView = this.FindControl<Modals.ManageProjectModalsView>("ModalsView");
        modalsView?.OpenReleaseFundsModal();
    }

    // ─────────────────────────────────────────────────────────────────
    //  SHARE MODAL
    // ─────────────────────────────────────────────────────────────────

    private void OnShareClick(object? sender, RoutedEventArgs e)
    {
        if (Vm?.Project == null) return;

        var shell = this.FindAncestorOfType<ShellView>();
        if (shell?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
        {
            var modal = new ShareModal(Vm.Project.Name, Vm.Project.Description);
            shellVm.ShowModal(modal);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  REFRESH
    // ─────────────────────────────────────────────────────────────────

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Vm != null)
                await Vm.RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OnRefreshClick failed");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  VIEW PRIVATE KEYS (shell modal)
    // ─────────────────────────────────────────────────────────────────

    private void OnViewPrivateKeysClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null) return;

        var shell = this.FindAncestorOfType<ShellView>();
        if (shell?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
        {
            // Skip password step — go directly to keys display (default password is used internally)
            var modal = new PrivateKeysDisplayModal(
                Vm.ProjectId, Vm.FounderKey, Vm.RecoveryKey,
                Vm.NostrNpub, Vm.Nip05, Vm.NostrNsec, Vm.NostrHex);
            shellVm.ShowModal(modal);
        }
    }
}
