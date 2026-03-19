using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia2.UI.Shared;
using Avalonia2.UI.Shared.Helpers;
using Avalonia2.UI.Shell;
using ReactiveUI;

namespace Avalonia2.UI.Sections.FindProjects;

public partial class InvestPageView : UserControl
{
    private IDisposable? _screenSubscription;
    private IDisposable? _layoutSubscription;
    private Border? _selectedQuickAmountBorder;
    private Border? _selectedSubPlanBorder;

    // Cached controls for responsive layout
    private DockPanel? _navBar;
    private Border? _footerBar;
    private StackPanel? _navStatsPills;
    private Grid? _contentGrid;
    private Border? _amountCard;
    private Border? _subscriptionCard;
    private Border? _stagesCard;
    private Border? _transactionCard;
    private Panel? _topSpacer;
    private Panel? _bottomSpacer;
    private UniformGrid? _mobileHeaderStats;
    private Border? _mobileSubmitButton;
    private ScrollViewer? _contentScroller;

    public InvestPageView()
    {
        InitializeComponent();

        // Cache controls once
        _navBar = this.FindControl<DockPanel>("NavBar");
        _footerBar = this.FindControl<Border>("FooterBar");
        _navStatsPills = this.FindControl<StackPanel>("NavStatsPills");
        _contentGrid = this.FindControl<Grid>("ContentGrid");
        _amountCard = this.FindControl<Border>("AmountCard");
        _subscriptionCard = this.FindControl<Border>("SubscriptionCard");
        _stagesCard = this.FindControl<Border>("StagesCard");
        _transactionCard = this.FindControl<Border>("TransactionCard");
        _topSpacer = this.FindControl<Panel>("TopSpacer");
        _bottomSpacer = this.FindControl<Panel>("BottomSpacer");
        _mobileHeaderStats = this.FindControl<UniformGrid>("MobileHeaderStats");
        _mobileSubmitButton = this.FindControl<Border>("MobileSubmitButton");
        _contentScroller = this.FindControl<ScrollViewer>("ContentScroller");

        // Wire up button clicks
        AddHandler(Button.ClickEvent, OnButtonClick);
        // Quick amount + submit + subscription plan border clicks
        AddHandler(Border.PointerPressedEvent, OnBorderPressed, RoutingStrategies.Bubble);

        // ── Responsive layout switching ──
        _layoutSubscription = LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact => ApplyResponsiveLayout(isCompact));
    }

    /// <summary>
    /// Apply responsive layout changes based on compact mode.
    /// Vue: @media (max-width: 768px) — nav hidden, content stacked, footer hidden,
    /// header stats shown, quick amounts 2-col, mobile submit visible, padding reduced.
    /// </summary>
    private void ApplyResponsiveLayout(bool isCompact)
    {
        // ── Nav bar: hidden on compact (Vue: .sticky-nav-bar { display: none } at ≤768px) ──
        if (_navBar != null)
            _navBar.IsVisible = !isCompact;

        // ── Footer bar: hidden on compact (Vue: .invest-fixed-footer { display: none } default) ──
        if (_footerBar != null)
            _footerBar.IsVisible = !isCompact;

        // ── Mobile header stats: shown on compact ──
        if (_mobileHeaderStats != null)
            _mobileHeaderStats.IsVisible = isCompact;

        // ── Mobile submit button: shown on compact ──
        if (_mobileSubmitButton != null)
            _mobileSubmitButton.IsVisible = isCompact;

        // ── Top spacer: 92px (desktop) → 16px (compact, Vue: padding-top: 16px) ──
        if (_topSpacer != null)
            _topSpacer.Height = isCompact ? 16 : 92;

        // ── Bottom spacer: 92px (desktop) → 120px (compact, Vue: padding-bottom: 120px for tab bar) ──
        if (_bottomSpacer != null)
            _bottomSpacer.Height = isCompact ? 120 : 92;

        // ── Content grid: 3-column → single column stacked ──
        if (_contentGrid != null)
        {
            if (isCompact)
            {
                // Vue: flex-direction: column, gap: 16px, padding: 0 16px 32px 16px
                _contentGrid.ColumnDefinitions.Clear();
                _contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _contentGrid.RowDefinitions.Clear();
                // 3 content rows (amount/sub, stages, transaction) with Auto height
                _contentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _contentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _contentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _contentGrid.ColumnSpacing = 0;
                _contentGrid.RowSpacing = 16; // Vue: gap: 16px on mobile
                _contentGrid.Margin = new Thickness(16, 0, 16, 16); // Vue: padding 0 16px 32px 16px (bottom handled by spacer)

                // Reposition cards to single column
                // Amount card (invest) — order 1
                if (_amountCard != null)
                {
                    Grid.SetColumn(_amountCard, 0);
                    Grid.SetRow(_amountCard, 0);
                    _amountCard.Padding = new Thickness(16); // Vue: .invest-card { padding: 16px } on mobile
                }
                // Subscription card — order 1 (same slot as amount, only one is visible)
                if (_subscriptionCard != null)
                {
                    Grid.SetColumn(_subscriptionCard, 0);
                    Grid.SetRow(_subscriptionCard, 0);
                    _subscriptionCard.Padding = new Thickness(16);
                }
                // Stages card — order 2
                if (_stagesCard != null)
                {
                    Grid.SetColumn(_stagesCard, 0);
                    Grid.SetRow(_stagesCard, 1);
                    _stagesCard.Padding = new Thickness(16);
                }
                // Transaction details card — order 3
                if (_transactionCard != null)
                {
                    Grid.SetColumn(_transactionCard, 0);
                    Grid.SetRow(_transactionCard, 2);
                    _transactionCard.Padding = new Thickness(16);
                }
            }
            else
            {
                // Restore 3-column desktop layout
                _contentGrid.ColumnDefinitions.Clear();
                _contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _contentGrid.RowDefinitions.Clear();
                _contentGrid.ColumnSpacing = 24;
                _contentGrid.RowSpacing = 24;
                _contentGrid.Margin = new Thickness(24, 0, 24, 24);

                // Restore column positions
                if (_amountCard != null)
                {
                    Grid.SetColumn(_amountCard, 0);
                    Grid.SetRow(_amountCard, 0);
                    _amountCard.Padding = new Thickness(24);
                }
                if (_subscriptionCard != null)
                {
                    Grid.SetColumn(_subscriptionCard, 0);
                    Grid.SetRow(_subscriptionCard, 0);
                    _subscriptionCard.Padding = new Thickness(24);
                }
                if (_stagesCard != null)
                {
                    Grid.SetColumn(_stagesCard, 1);
                    Grid.SetRow(_stagesCard, 0);
                    _stagesCard.Padding = new Thickness(24);
                }
                if (_transactionCard != null)
                {
                    Grid.SetColumn(_transactionCard, 2);
                    Grid.SetRow(_transactionCard, 0);
                    _transactionCard.Padding = new Thickness(24);
                }
            }
        }

        // ── Quick amounts: 4 columns → 2 columns on compact ──
        // Vue: .quick-amounts { grid-template-columns: repeat(2, 1fr) } at ≤768px
        // The UniformGrid is inside an ItemsPanelTemplate — find it by visual tree walk
        UpdateQuickAmountsColumns(isCompact ? 2 : 4);
    }

    /// <summary>
    /// Find the UniformGrid inside the quick amounts ItemsControl and set column count.
    /// </summary>
    private void UpdateQuickAmountsColumns(int columns)
    {
        // Walk visual descendants to find the UniformGrid inside the quick amounts ItemsControl
        foreach (var ug in this.GetVisualDescendants().OfType<UniformGrid>())
        {
            // The quick amounts UniformGrid is the one that's NOT our MobileHeaderStats
            if (ug != _mobileHeaderStats)
            {
                ug.Columns = columns;
                break; // Only one other UniformGrid in this view
            }
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Reset scroll to top when navigating to a new invest page
        _contentScroller?.ScrollToHome();

        _screenSubscription?.Dispose();
        _screenSubscription = null;

        if (DataContext is InvestPageViewModel vm)
        {
            // Watch for screen changes to show/hide shell-level modal
            _screenSubscription = vm.WhenAnyValue(x => x.CurrentScreen)
                .Subscribe(screen =>
                {
                    if (screen != InvestScreen.InvestForm)
                    {
                        ShowShellModal(vm);
                    }
                });

            // If subscription, apply initial plan selection styling after layout.
            // This one-time walk finds the initially-selected plan's border.
            if (vm.IsSubscription)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // Find the SubPlanBorder whose DataContext has IsSelected == true
                    foreach (var border in this.GetVisualDescendants().OfType<Border>())
                    {
                        if (border.Name == "SubPlanBorder" &&
                            border.DataContext is SubscriptionPlanOption { IsSelected: true })
                        {
                            UpdateSubscriptionPlanSelection(border);
                            break;
                        }
                    }
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        _screenSubscription?.Dispose();
        _screenSubscription = null;
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
    }

    private InvestPageViewModel? Vm => DataContext as InvestPageViewModel;

    private ShellViewModel? GetShellVm()
    {
        var shellView = this.FindAncestorOfType<ShellView>();
        return shellView?.DataContext as ShellViewModel;
    }

    /// <summary>
    /// Create InvestModalsView and push it to the shell-level modal overlay.
    /// </summary>
    private void ShowShellModal(InvestPageViewModel vm)
    {
        var shellVm = GetShellVm();
        if (shellVm == null || shellVm.IsModalOpen) return;

        var modalsView = new InvestModalsView
        {
            DataContext = vm,
            OnNavigateBackToList = () =>
            {
                // Add the invested project to the Portfolio section
                vm.AddToPortfolio();
                // Navigate to the Funded section to show the new investment
                var shell = GetShellVm();
                shell?.NavigateToFunded();
            }
        };

        shellVm.ShowModal(modalsView);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            // Back button (standardized: Button inside Border)
            case "BackButton":
                NavigateBackToDetail();
                break;
        }
    }

    /// <summary>
    /// Handle clicks on Border elements — quick amounts, submit button, copy project ID,
    /// mobile submit button, and subscription plan buttons.
    /// </summary>
    private void OnBorderPressed(object? sender, PointerPressedEventArgs e)
    {
        var source = e.Source as Control;
        Border? found = null;
        string? foundName = null;

        while (source != null)
        {
            if (source is Border b && !string.IsNullOrEmpty(b.Name))
            {
                var name = b.Name;
                if (name == "QuickAmountBorder" || name == "SubmitButton" ||
                    name == "CopyProjectIdButton" || name == "SubPlanBorder" ||
                    name == "MobileSubmitButton")
                {
                    found = b;
                    foundName = name;
                    break;
                }
            }
            source = source.Parent as Control;
        }

        if (found == null || foundName == null) return;

        switch (foundName)
        {
            case "SubmitButton":
            case "MobileSubmitButton":
                Vm?.Submit();
                e.Handled = true;
                break;

            case "QuickAmountBorder":
                if (found.DataContext is QuickAmountOption option)
                {
                    Vm?.SelectQuickAmount(option.Amount);
                    UpdateQuickAmountSelection(found);
                    e.Handled = true;
                }
                break;

            case "SubPlanBorder":
                if (found.DataContext is SubscriptionPlanOption plan)
                {
                    Vm?.SelectSubscriptionPlan(plan.PatternId);
                    UpdateSubscriptionPlanSelection(found);
                    e.Handled = true;
                }
                break;

            case "CopyProjectIdButton":
                ClipboardHelper.CopyToClipboard(this, Vm?.ProjectId);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Navigate back to project detail (go up one drill-down level).</summary>
    private void NavigateBackToDetail()
    {
        var findProjectsView = this.FindLogicalAncestorOfType<FindProjectsView>();
        if (findProjectsView?.DataContext is FindProjectsViewModel vm)
        {
            vm.CloseInvestPage();
        }
    }

    /// <summary>Update quick amount borders via CSS class toggling.
    /// Tracks previously-selected border to avoid full tree walk.</summary>
    private void UpdateQuickAmountSelection(Border newSelected)
    {
        // Deselect previous
        _selectedQuickAmountBorder?.Classes.Set("QuickAmountSelected", false);
        // Select new
        newSelected.Classes.Set("QuickAmountSelected", true);
        _selectedQuickAmountBorder = newSelected;
    }

    /// <summary>Update subscription plan borders via CSS class toggling.
    /// Tracks previously-selected border to avoid full tree walk.</summary>
    private void UpdateSubscriptionPlanSelection(Border newSelected)
    {
        // Deselect previous
        _selectedSubPlanBorder?.Classes.Set("SubPlanSelected", false);
        // Select new
        newSelected.Classes.Set("SubPlanSelected", true);
        _selectedSubPlanBorder = newSelected;
    }
}
