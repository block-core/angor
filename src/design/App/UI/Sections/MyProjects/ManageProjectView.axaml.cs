using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using App.UI.Shared.Controls;
using App.UI.Shell;

namespace App.UI.Sections.MyProjects;

public partial class ManageProjectView : UserControl
{
    private ManageProjectViewModel? Vm => DataContext as ManageProjectViewModel;

    public ManageProjectView()
    {
        InitializeComponent();

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

        // ── Refresh button ──
        var refreshBtn = this.FindControl<Button>("RefreshButton");
        if (refreshBtn != null) refreshBtn.Click += OnRefreshClick;

        // ── View Private Keys button (opens shell modal password step) ──
        var viewPKBtn = this.FindControl<Button>("ViewPrivateKeysButton");
        if (viewPKBtn != null) viewPKBtn.Click += OnViewPrivateKeysClick;
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

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (Vm != null)
            await Vm.RefreshAsync();
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
            // Skip password modal — password is not used (SimplePasswordProvider returns "default-key").
            // Open the keys display modal directly.
            var modal = new PrivateKeysDisplayModal(
                Vm.ProjectId, Vm.FounderKey, Vm.RecoveryKey,
                Vm.NostrNpub, Vm.Nip05, Vm.NostrNsec, Vm.NostrHex);
            shellVm.ShowModal(modal);
        }
    }
}
