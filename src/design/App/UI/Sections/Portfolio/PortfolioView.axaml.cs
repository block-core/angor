using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
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

        // ── Cache responsive layout controls ──
        _portfolioGrid = this.FindControl<Grid>("PortfolioListPanel");
        _sidebar = this.FindControl<Border>("PortfolioSidebar");
        _content = this.FindControl<ScrollableView>("PortfolioContent");
        _investmentDetailPanel = this.FindControl<Panel>("InvestmentDetailPanel");

        // ── Responsive layout: 380px sidebar + content (desktop) → stacked (compact) ──
        _layoutSubscription = LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact => ApplyResponsiveLayout(isCompact));

        // ── Mobile perf: detach the sidebar from the PortfolioListPanel grid.
        // On compact layout the sidebar is hidden above the fold with all its
        // children (SVG logo, 4 stat cards, 2 buttons) still costing
        // measure/arrange. Detaching skips that entirely. Re-insert on
        // ApplicationIdle so it's ready before the user can interact.
        // Also strip hover transitions on investment card borders — they
        // allocate animators that never fire on touch-only platforms.
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            if (_portfolioGrid != null && _sidebar != null)
            {
                var sidebarIndex = _portfolioGrid.Children.IndexOf(_sidebar);
                if (sidebarIndex >= 0)
                {
                    _portfolioGrid.Children.RemoveAt(sidebarIndex);
                    var gridRef = _portfolioGrid;
                    var sidebarRef = _sidebar;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        var safeIdx = Math.Min(sidebarIndex, gridRef.Children.Count);
                        gridRef.Children.Insert(safeIdx, sidebarRef);
                    }, Avalonia.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
        }
    }

    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_portfolioGrid == null || _sidebar == null || _content == null) return;

        // CRITICAL: modify existing column/row widths in-place — never Clear()+Add().
        // XAML Grid always has 2 columns and 2 rows:
        //   Desktop:  Col0=380 (sidebar), Col1=* (content) | Row0=* (content), Row1=0
        //   Compact:  Col0=* (full width), Col1=0 (hidden)  | Row0=Auto (sidebar), Row1=* (content)
        var cols = _portfolioGrid.ColumnDefinitions;
        var rows = _portfolioGrid.RowDefinitions;

        if (isCompact)
        {
            if (cols.Count >= 2) { cols[0].Width = GridLength.Star; cols[1].Width = new GridLength(0); }
            if (rows.Count >= 2) { rows[0].Height = GridLength.Auto; rows[1].Height = GridLength.Star; }

            Grid.SetColumn(_sidebar, 0);
            Grid.SetRow(_sidebar, 0);
            _sidebar.Margin = new Avalonia.Thickness(0, 0, 0, 24);
            _sidebar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

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
                (hasInvestments, selected) => (hasInvestments, selected))
              .Subscribe(tuple =>
              {
                  var (hasInvestments, selected) = tuple;
                  var visible = hasInvestments && selected == null;

                  if (PortfolioListPanel != null)
                      PortfolioListPanel.IsVisible = visible;

                  // Lazy-mount InvestmentDetailView on first drill-down
                  if (_investmentDetailPanel != null)
                  {
                      if (selected != null)
                      {
                          if (_investmentDetailView == null)
                          {
                              _investmentDetailView = new InvestmentDetailView { DataContext = selected };
                              _investmentDetailPanel.Children.Add(_investmentDetailView);
                          }
                          else
                          {
                              _investmentDetailView.DataContext = selected;
                          }
                      }
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
