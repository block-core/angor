using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using App.UI.Shared;
using App.UI.Shared.Helpers;
using ReactiveUI;

namespace App.UI.Sections.MyProjects;

/// <summary>
/// Contains the main content sections of ManageProjectView:
/// Project ID card, Project Statistics, Next Stage + Transaction Statistics, Stages.
/// Shares DataContext (ManageProjectViewModel) with parent via inheritance.
/// Stage button clicks bubble up via RoutingStrategies.Bubble to the parent.
/// </summary>
public partial class ManageProjectContentView : UserControl
{
    private IDisposable? _layoutSubscription;

    // Cached responsive layout controls
    private Grid? _manageStatsGrid;
    private Border? _manageStatCard0;
    private Border? _manageStatCard1;
    private Border? _manageStatCard2;
    private Border? _manageStatCard3;
    private Grid? _manageStatsRowGrid;
    private Border? _manageNextStageCard;
    private Border? _manageTxStatsCard;
    private StackPanel? _contentStack;
    private Grid? _projectIdGrid;
    private StackPanel? _projectIdActions;

    public ManageProjectContentView()
    {
        InitializeComponent();

        // Stage card buttons use routed event bubbling — attach handler on the
        // ItemsControl so clicks on Claim/Spent buttons inside the DataTemplate
        // bubble up. The parent ManageProjectView catches these to open modals.
        var stagesCtrl = this.FindControl<ItemsControl>("StagesItemsControl");
        stagesCtrl?.AddHandler(Button.ClickEvent, OnStageButtonClick, RoutingStrategies.Bubble);

        // Wire copy project ID button
        // Vue: copyToClipboard(projectId) — .copy-button in ManageFunds.vue
        var copyBtn = this.FindControl<Button>("CopyProjectIdBtn");
        if (copyBtn != null)
            copyBtn.Click += (_, _) =>
            {
                if (DataContext is ManageProjectViewModel vm)
                    ClipboardHelper.CopyToClipboard(this, vm.ProjectId);
            };

        // Wire Edit Project button
        var editBtn = this.FindControl<Button>("EditProjectButton");
        if (editBtn != null)
            editBtn.Click += (_, _) => EditProjectRequested?.Invoke();

        // Cache responsive controls
        _manageStatsGrid = this.FindControl<Grid>("ManageStatsGrid");
        _manageStatCard0 = this.FindControl<Border>("ManageStatCard0");
        _manageStatCard1 = this.FindControl<Border>("ManageStatCard1");
        _manageStatCard2 = this.FindControl<Border>("ManageStatCard2");
        _manageStatCard3 = this.FindControl<Border>("ManageStatCard3");
        _manageStatsRowGrid = this.FindControl<Grid>("ManageStatsRowGrid");
        _manageNextStageCard = this.FindControl<Border>("ManageNextStageCard");
        _manageTxStatsCard = this.FindControl<Border>("ManageTxStatsCard");
        _contentStack = this.FindControl<StackPanel>("ContentStack");
        _projectIdGrid = this.FindControl<Grid>("ProjectIdGrid");
        _projectIdActions = this.FindControl<StackPanel>("ProjectIdActions");

        // Subscribe to layout mode changes
        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);
    }

    /// <summary>
    /// Responsive layout: compact → stats stack single column, side-by-side → stacked,
    /// stage pills stack vertically, bottom padding for tab bar clearance.
    /// Vue: <=1024px → stats repeat(2,1fr), stats-row 1fr; <=640px → stats 1fr.
    /// Vue: <=768px → .stage-header-left column, .stage-pills column, padding-bottom 96px.
    /// We use IsCompact (<=1024px) → 1-col stacked for both.
    ///
    /// IMPORTANT: Grids pre-declare max column/row counts in XAML. This method only
    /// mutates existing <c>GridLength</c> values — never replaces the ColumnDefinitions
    /// or RowDefinitions collections. Replacing those causes Avalonia's layout engine
    /// to crash (SIGABRT on macOS) because children briefly reference invalid indices.
    /// Pattern established in c19cd35f.
    /// </summary>
    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_manageStatsGrid == null) return;

        // Toggle CSS class for style-selector-driven changes (stage pills, etc.)
        Classes.Set("Compact", isCompact);

        // Bottom padding for tab bar clearance
        // Vue: <=768px → .content-grid { padding-bottom: 96px }
        if (_contentStack != null)
            _contentStack.Margin = isCompact ? new Thickness(0, 0, 0, 96) : new Thickness(0);

        // ── Project ID Grid ──
        // XAML: ColumnDefinitions="*,Auto" RowDefinitions="Auto,0,0"
        //   Desktop: col 0 = info, col 1 = actions, rows 1-2 collapsed
        //   Mobile:  col 0 spans both, actions drop to row 2 (row 1 is 12px gap)
        if (_projectIdGrid != null && _projectIdActions != null)
        {
            var pCols = _projectIdGrid.ColumnDefinitions;
            var pRows = _projectIdGrid.RowDefinitions;
            if (isCompact)
            {
                if (pCols.Count >= 2) { pCols[0].Width = GridLength.Star; pCols[1].Width = new GridLength(0); }
                if (pRows.Count >= 3) { pRows[0].Height = GridLength.Auto; pRows[1].Height = new GridLength(12); pRows[2].Height = GridLength.Auto; }
                Grid.SetColumn(_projectIdActions, 0);
                Grid.SetRow(_projectIdActions, 2);
                _projectIdActions.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            }
            else
            {
                if (pCols.Count >= 2) { pCols[0].Width = GridLength.Star; pCols[1].Width = GridLength.Auto; }
                if (pRows.Count >= 3) { pRows[0].Height = GridLength.Auto; pRows[1].Height = new GridLength(0); pRows[2].Height = new GridLength(0); }
                Grid.SetColumn(_projectIdActions, 1);
                Grid.SetRow(_projectIdActions, 0);
                _projectIdActions.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            }
        }

        // ── Manage Stats Grid (4 stat cards) ──
        // XAML: ColumnDefinitions="*,*,*,*" RowDefinitions="Auto,0,0,0"
        //   Desktop: 4 cards side-by-side in row 0
        //   Mobile:  4 cards stacked vertically in col 0, rows 0/1/2/3
        {
            var sCols = _manageStatsGrid.ColumnDefinitions;
            var sRows = _manageStatsGrid.RowDefinitions;
            if (isCompact)
            {
                if (sCols.Count >= 4)
                {
                    sCols[0].Width = GridLength.Star;
                    sCols[1].Width = new GridLength(0);
                    sCols[2].Width = new GridLength(0);
                    sCols[3].Width = new GridLength(0);
                }
                if (sRows.Count >= 4)
                {
                    sRows[0].Height = GridLength.Auto;
                    sRows[1].Height = GridLength.Auto;
                    sRows[2].Height = GridLength.Auto;
                    sRows[3].Height = GridLength.Auto;
                }
                SetCardCompact(_manageStatCard0, 0);
                SetCardCompact(_manageStatCard1, 1);
                SetCardCompact(_manageStatCard2, 2);
                SetCardCompact(_manageStatCard3, 3);
            }
            else
            {
                if (sCols.Count >= 4)
                {
                    sCols[0].Width = GridLength.Star;
                    sCols[1].Width = GridLength.Star;
                    sCols[2].Width = GridLength.Star;
                    sCols[3].Width = GridLength.Star;
                }
                if (sRows.Count >= 4)
                {
                    sRows[0].Height = GridLength.Auto;
                    sRows[1].Height = new GridLength(0);
                    sRows[2].Height = new GridLength(0);
                    sRows[3].Height = new GridLength(0);
                }
                SetCardDesktop(_manageStatCard0, 0, new Thickness(0, 0, 8, 0));
                SetCardDesktop(_manageStatCard1, 1, new Thickness(8, 0, 8, 0));
                SetCardDesktop(_manageStatCard2, 2, new Thickness(8, 0, 8, 0));
                SetCardDesktop(_manageStatCard3, 3, new Thickness(8, 0, 0, 0));
            }
        }

        // ── Stats Row Grid (Next Stage + Transaction Stats) ──
        // XAML: ColumnDefinitions="*,24,*" RowDefinitions="Auto,0,0"
        //   Desktop: col 0 = NextStage, col 2 = TxStats, rows 1-2 collapsed
        //   Mobile:  col 0 = NextStage, row 0; TxStats row 2 (row 1 = 24px gap)
        if (_manageStatsRowGrid != null)
        {
            var rCols = _manageStatsRowGrid.ColumnDefinitions;
            var rRows = _manageStatsRowGrid.RowDefinitions;
            if (isCompact)
            {
                if (rCols.Count >= 3) { rCols[0].Width = GridLength.Star; rCols[1].Width = new GridLength(0); rCols[2].Width = new GridLength(0); }
                if (rRows.Count >= 3) { rRows[0].Height = GridLength.Auto; rRows[1].Height = new GridLength(24); rRows[2].Height = GridLength.Auto; }
                if (_manageNextStageCard != null) { Grid.SetColumn(_manageNextStageCard, 0); Grid.SetRow(_manageNextStageCard, 0); }
                if (_manageTxStatsCard != null) { Grid.SetColumn(_manageTxStatsCard, 0); Grid.SetRow(_manageTxStatsCard, 2); }
            }
            else
            {
                if (rCols.Count >= 3) { rCols[0].Width = GridLength.Star; rCols[1].Width = new GridLength(24); rCols[2].Width = GridLength.Star; }
                if (rRows.Count >= 3) { rRows[0].Height = GridLength.Auto; rRows[1].Height = new GridLength(0); rRows[2].Height = new GridLength(0); }
                if (_manageNextStageCard != null) { Grid.SetColumn(_manageNextStageCard, 0); Grid.SetRow(_manageNextStageCard, 0); }
                if (_manageTxStatsCard != null) { Grid.SetColumn(_manageTxStatsCard, 2); Grid.SetRow(_manageTxStatsCard, 0); }
            }
        }
    }

    private static void SetCardCompact(Border? card, int row)
    {
        if (card == null) return;
        Grid.SetColumn(card, 0);
        Grid.SetRow(card, row);
        card.Margin = new Thickness(0, row > 0 ? 12 : 0, 0, 0);
    }

    private static void SetCardDesktop(Border? card, int col, Thickness margin)
    {
        if (card == null) return;
        Grid.SetColumn(card, col);
        Grid.SetRow(card, 0);
        card.Margin = margin;
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    /// <summary>
    /// Raised when a stage Claim or Spent button is clicked.
    /// The parent ManageProjectView subscribes to this to open the appropriate modal.
    /// </summary>
    public event System.Action<int, string>? StageButtonClicked;

    /// <summary>
    /// Raised when the Edit Project button is clicked.
    /// The parent wires this to navigate to the edit profile view.
    /// </summary>
    public event System.Action? EditProjectRequested;
    private void OnStageButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        if (btn.Classes.Contains("StageClaimBtn") && btn.Tag is int claimStageNum)
        {
            StageButtonClicked?.Invoke(claimStageNum - 1, "Claim");
            e.Handled = true;
        }
        else if (btn.Classes.Contains("StageSpentBtn") && btn.Tag is int spentStageNum)
        {
            StageButtonClicked?.Invoke(spentStageNum - 1, "Spent");
            e.Handled = true;
        }
    }
}
