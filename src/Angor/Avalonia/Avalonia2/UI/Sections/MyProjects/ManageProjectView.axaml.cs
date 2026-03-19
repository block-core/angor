using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia2.UI.Shared;
using Avalonia2.UI.Shared.Controls;
using Avalonia2.UI.Shell;
using ReactiveUI;

namespace Avalonia2.UI.Sections.MyProjects;

public partial class ManageProjectView : UserControl
{
    private ManageProjectViewModel? Vm => DataContext as ManageProjectViewModel;
    private IDisposable? _layoutSubscription;

    // Cached responsive controls
    private DockPanel? _navBar;
    private Panel? _navSpacer;

    public ManageProjectView()
    {
        InitializeComponent();

        // Cache responsive controls
        _navBar = this.FindControl<DockPanel>("ManageNavBar");
        _navSpacer = this.FindControl<Panel>("ManageNavSpacer");

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
        }

        // ── Share button ──
        var shareBtn = this.FindControl<Button>("ShareButton");
        if (shareBtn != null) shareBtn.Click += OnShareClick;

        // ── View Private Keys button (opens shell modal password step) ──
        var viewPKBtn = this.FindControl<Button>("ViewPrivateKeysButton");
        if (viewPKBtn != null) viewPKBtn.Click += OnViewPrivateKeysClick;

        // Subscribe to layout mode changes
        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);
    }

    /// <summary>
    /// Responsive layout: compact → hide nav bar (bottom tab bar provides navigation).
    /// Vue: desktop nav hidden md:flex, mobile nav md:hidden.
    /// </summary>
    private void ApplyResponsiveLayout(bool isCompact)
    {
        // Hide desktop nav bar on compact — the shell's bottom tab bar provides navigation
        // Vue: .sticky-nav-bar { display: none !important; } at <=768px
        if (_navBar != null) _navBar.IsVisible = !isCompact;

        // Adjust spacer: no nav bar means no spacer needed
        if (_navSpacer != null) _navSpacer.Height = isCompact ? 0 : 92;
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
    //  VIEW PRIVATE KEYS (shell modal)
    // ─────────────────────────────────────────────────────────────────

    private void OnViewPrivateKeysClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null) return;

        var shell = this.FindAncestorOfType<ShellView>();
        if (shell?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
        {
            var modal = new PrivateKeysPasswordModal(
                Vm.ProjectId, Vm.FounderKey, Vm.RecoveryKey,
                Vm.NostrNpub, Vm.Nip05, Vm.NostrNsec, Vm.NostrHex);
            shellVm.ShowModal(modal);
        }
    }
}
