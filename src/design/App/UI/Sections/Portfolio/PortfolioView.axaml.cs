using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using App.UI.Shared;
using App.UI.Shared.Controls;
using App.UI.Shell;
using System.Reactive.Linq;

namespace App.UI.Sections.Portfolio;

public partial class PortfolioView : UserControl, ISectionView
{
    private IDisposable? _visibilitySubscription;
    private IDisposable? _layoutSubscription;

    // Cached controls for responsive layout
    private Grid? _portfolioGrid;
    private Border? _sidebar;
    private Border? _mobileHeader;
    private ScrollableView? _content;

    // Lazy-mounted drill-down child — materialised on first visibility
    // to avoid the ~1258-line XAML inflate cost on the initial tab switch.
    private Panel? _investmentDetailPanel;
    private InvestmentDetailView? _investmentDetailView;

    /// <summary>Design-time only.</summary>
    public PortfolioView() => InitializeComponent();

    public PortfolioView(PortfolioViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // Tag as Mobile so style sheet can strip hover transitions + BoxShadow
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            Classes.Add("Mobile");

        // When navigating back to Funded, clear any open detail view
        // so the user sees the list (not a stale detail screen from last time).
        vm.CloseInvestmentDetail();

        // Manage visibility of the portfolio list panel based on ViewModel state
        DataContextChanged += (_, _) => SubscribeToVisibility();
        SubscribeToVisibility();

        // Wire Penalties button to open shell modal
        var penaltiesBtn = this.FindControl<Button>("PenaltiesButton");
        if (penaltiesBtn != null) penaltiesBtn.Click += OnPenaltiesClick;
        var mobilePenaltiesBtn = this.FindControl<Button>("MobilePenaltiesButton");
        if (mobilePenaltiesBtn != null) mobilePenaltiesBtn.Click += OnPenaltiesClick;

        // ── Cache responsive layout controls ──
        _portfolioGrid = this.FindControl<Grid>("PortfolioListPanel");
        _sidebar = this.FindControl<Border>("PortfolioSidebar");
        _mobileHeader = this.FindControl<Border>("MobilePortfolioHeader");
        _content = this.FindControl<ScrollableView>("PortfolioContent");
        _investmentDetailPanel = this.FindControl<Panel>("InvestmentDetailPanel");

        // ── Responsive layout: 380px sidebar + content (desktop) → stacked (compact) ──
        _layoutSubscription = LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact => ApplyResponsiveLayout(isCompact));

        // Mobile perf: sidebar is hidden via IsVisible=false on compact,
        // so no need to detach children. Strip hover transitions via .Mobile class.
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            // .Mobile class already added above — strips hover transitions + BoxShadow
        }
    }

    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_portfolioGrid == null || _sidebar == null || _content == null) return;

        // Toggle sidebar vs compact mobile header
        _sidebar.IsVisible = !isCompact;
        if (_mobileHeader != null)
            _mobileHeader.IsVisible = isCompact;

        // CRITICAL: modify existing column/row widths in-place — never Clear()+Add().
        var cols = _portfolioGrid.ColumnDefinitions;
        var rows = _portfolioGrid.RowDefinitions;

        if (isCompact)
        {
            if (cols.Count >= 2) { cols[0].Width = GridLength.Star; cols[1].Width = new GridLength(0); }
            if (rows.Count >= 2) { rows[0].Height = new GridLength(0); rows[1].Height = GridLength.Star; }

            Grid.SetColumn(_content, 0);
            Grid.SetRow(_content, 1);
            _content.ContentPadding = new Avalonia.Thickness(0, 0, 0, 96);
        }
        else
        {
            if (cols.Count >= 2) { cols[0].Width = new GridLength(380); cols[1].Width = GridLength.Star; }
            if (rows.Count >= 2) { rows[0].Height = GridLength.Star; rows[1].Height = new GridLength(0); }

            Grid.SetColumn(_sidebar, 0);
            Grid.SetRow(_sidebar, 0);
            _sidebar.Margin = new Avalonia.Thickness(0, 0, 24, 0);
            _sidebar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;

            Grid.SetColumn(_content, 1);
            Grid.SetRow(_content, 0);
            _content.ContentPadding = new Avalonia.Thickness(0, 0, 16, 0);
        }
    }

    private void SubscribeToVisibility()
    {
        _visibilitySubscription?.Dispose();

        if (DataContext is PortfolioViewModel vm)
        {
            vm.ToastRequested -= OnToastRequested;
            _visibilitySubscription = System.Reactive.Disposables.Disposable.Create(() => vm.ToastRequested -= OnToastRequested);
            vm.ToastRequested += OnToastRequested;

            // Portfolio list is visible when: HasInvestments AND no detail selected.
            // HasInvestments is handled by XAML binding; here we also hide when
            // SelectedInvestment is set (drill-down to detail view).
            var visibilitySub = vm.WhenAnyValue(
                x => x.HasInvestments,
                x => x.SelectedInvestment,
                x => x.IsInitialLoad,
                (hasInvestments, selected, isInitialLoad) => (hasInvestments, selected, isInitialLoad))
              .Subscribe(tuple =>
              {
                  var (hasInvestments, selected, isInitialLoad) = tuple;
                  // Show portfolio list when: loading skeletons OR has real investments (and no detail open)
                  var visible = (hasInvestments || isInitialLoad) && selected == null;

                  if (PortfolioListPanel != null)
                      PortfolioListPanel.IsVisible = visible;

                  // Empty state: only when not loading AND no investments AND no detail open
                  var emptyState = this.FindControl<Control>("EmptyStatePanel");
                  if (emptyState != null)
                      emptyState.IsVisible = !hasInvestments && !isInitialLoad && selected == null;

                  // Lazy-mount InvestmentDetailView on first drill-down
                  if (_investmentDetailPanel != null)
                  {
                      if (selected != null)
                      {
                          if (_investmentDetailView == null)
                          {
                              // Show skeleton immediately while real view inflates
                              var skeleton = new SkeletonInvestmentDetailView();
                              _investmentDetailPanel.Children.Add(skeleton);
                              _investmentDetailPanel.IsVisible = true;

                              var selectedVm = selected;
                              Dispatcher.UIThread.Post(() =>
                              {
                                  _investmentDetailView = new InvestmentDetailView { DataContext = selectedVm };
                                  _investmentDetailPanel.Children.Remove(skeleton);
                                  _investmentDetailPanel.Children.Add(_investmentDetailView);
                              }, DispatcherPriority.Background);
                          }
                          else
                          {
                              _investmentDetailView.DataContext = selected;
                          }
                      }
                      // Don't hide while skeleton→real swap is in flight
                      if (!(_investmentDetailView == null && selected != null))
                          _investmentDetailPanel.IsVisible = selected != null;
                  }

                  // Publish detail view state to ShellViewModel for mobile back-button visibility
                  var shell = this.FindAncestorOfType<ShellView>();
                  if (shell?.DataContext is ShellViewModel shellVm)
                      shellVm.IsInvestmentDetailOpen = selected != null;
              });

            _visibilitySubscription = new System.Reactive.Disposables.CompositeDisposable(_visibilitySubscription, visibilitySub);
        }
    }

    private void OnToastRequested(string message)
    {
        var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
        shellVm?.ShowToast(message);
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        // Re-subscribe when the cached view is re-added to the tree
        // (the subscription was disposed in OnDetachedFromLogicalTree).
        SubscribeToVisibility();

        // Auto-refresh investments when navigating back to portfolio
        // (e.g. after investing, stages need to be reloaded from SDK)
        // On mobile with SectionPanel, OnBecameActive() handles this instead.
        if (DataContext is PortfolioViewModel vm && !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
            _ = vm.LoadInvestmentsFromSdkAsync();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _visibilitySubscription?.Dispose();
        _visibilitySubscription = null;
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    public void OnBecameActive()
    {
        if (DataContext is PortfolioViewModel vm)
        {
            vm.CloseInvestmentDetail();
            _ = vm.LoadInvestmentsFromSdkAsync();
        }
    }

    public void OnBecameInactive() { }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "RefreshButton":
            case "MobileRefreshButton":
                if (DataContext is PortfolioViewModel refreshVm)
                    _ = refreshVm.LoadInvestmentsFromSdkAsync();
                e.Handled = true;
                break;

            case "ManageButton" when btn.Tag is InvestmentViewModel investment:
                if (DataContext is PortfolioViewModel vm)
                    vm.OpenInvestmentDetail(investment);
                break;
        }
    }

    private void OnPenaltiesClick(object? sender, RoutedEventArgs e)
    {
        var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
        var vm = DataContext as PortfolioViewModel;
        shellVm?.ShowModal(new PenaltiesModal(vm!));
    }
}
