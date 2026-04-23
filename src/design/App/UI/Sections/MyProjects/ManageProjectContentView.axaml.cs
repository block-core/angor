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

        // Project ID card: on mobile stack actions below info (single column, two rows).
        // Vue mobile: .project-id-card content flex-direction: column.
        if (_projectIdGrid != null && _projectIdActions != null)
        {
            _projectIdGrid.ColumnDefinitions.Clear();
            _projectIdGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            if (isCompact)
            {
                // Add a second column so Auto doesn't collapse — we just put everything in col 0.
                _projectIdGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                _projectIdGrid.RowDefinitions.Clear();
                _projectIdGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _projectIdGrid.RowDefinitions.Add(new RowDefinition(new GridLength(12)));
                _projectIdGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetColumn(_projectIdActions, 0);
                Grid.SetRow(_projectIdActions, 2);
            }
            else
            {
                _projectIdGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                _projectIdGrid.RowDefinitions.Clear();
                Grid.SetColumn(_projectIdActions, 1);
                Grid.SetRow(_projectIdActions, 0);
            }
        }

        if (isCompact)
        {
            // Stats grid: single column stacked
            _manageStatsGrid.ColumnDefinitions.Clear();
            _manageStatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _manageStatsGrid.RowDefinitions.Clear();
            for (int i = 0; i < 4; i++)
                _manageStatsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            SetCardCompact(_manageStatCard0, 0);
            SetCardCompact(_manageStatCard1, 1);
            SetCardCompact(_manageStatCard2, 2);
            SetCardCompact(_manageStatCard3, 3);

            // Stats row: stack Next Stage on top of Transaction Stats
            // Vue: <=1024px → grid-template-columns: 1fr
            if (_manageStatsRowGrid != null)
            {
                _manageStatsRowGrid.ColumnDefinitions.Clear();
                _manageStatsRowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _manageStatsRowGrid.RowDefinitions.Clear();
                _manageStatsRowGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _manageStatsRowGrid.RowDefinitions.Add(new RowDefinition(new GridLength(24)));
                _manageStatsRowGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                if (_manageNextStageCard != null)
                {
                    Grid.SetColumn(_manageNextStageCard, 0);
                    Grid.SetRow(_manageNextStageCard, 0);
                }
                if (_manageTxStatsCard != null)
                {
                    Grid.SetColumn(_manageTxStatsCard, 0);
                    Grid.SetRow(_manageTxStatsCard, 2);
                }
            }
        }
        else
        {
            // Stats grid: 4 columns
            _manageStatsGrid.ColumnDefinitions.Clear();
            for (int i = 0; i < 4; i++)
                _manageStatsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _manageStatsGrid.RowDefinitions.Clear();

            SetCardDesktop(_manageStatCard0, 0, new Thickness(0, 0, 8, 0));
            SetCardDesktop(_manageStatCard1, 1, new Thickness(8, 0, 8, 0));
            SetCardDesktop(_manageStatCard2, 2, new Thickness(8, 0, 8, 0));
            SetCardDesktop(_manageStatCard3, 3, new Thickness(8, 0, 0, 0));

            // Stats row: side by side (1fr, 24px spacer, 1fr)
            if (_manageStatsRowGrid != null)
            {
                _manageStatsRowGrid.ColumnDefinitions.Clear();
                _manageStatsRowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _manageStatsRowGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(24)));
                _manageStatsRowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _manageStatsRowGrid.RowDefinitions.Clear();

                if (_manageNextStageCard != null)
                {
                    Grid.SetColumn(_manageNextStageCard, 0);
                    Grid.SetRow(_manageNextStageCard, 0);
                }
                if (_manageTxStatsCard != null)
                {
                    Grid.SetColumn(_manageTxStatsCard, 2);
                    Grid.SetRow(_manageTxStatsCard, 0);
                }
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
