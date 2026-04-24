using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using App.UI.Shared;
using App.UI.Shared.Controls;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reactive.Linq;

namespace App.UI.Sections.FindProjects;

public partial class FindProjectsView : UserControl, ISectionView
{
    private IDisposable? _visibilitySubscription;
    private IDisposable? _layoutSubscription;

    // Cached FindControl results — avoid repeated tree walks on every visibility update
    private Panel? _detailPanel;
    private Panel? _investPanel;
    private ScrollableView? _projectListScrollable;
    private ScrollViewer? _listScrollViewer;

    // Lazy-mounted drill-down children — materialised on first visibility
    // to avoid the ~1800-line XAML inflate cost on the initial tab switch.
    private ProjectDetailView? _projectDetailView;
    private InvestPageView? _investPageView;

    /// <summary>Design-time only.</summary>
    public FindProjectsView() => InitializeComponent();

    public FindProjectsView(FindProjectsViewModel vm)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        InitializeComponent();
        var initMs = sw.ElapsedMilliseconds;

        sw.Restart();
        DataContext = vm;
        var dcMs = sw.ElapsedMilliseconds;

        sw.Restart();
        // Cache panels once
        _detailPanel = this.FindControl<Panel>("ProjectDetailPanel");
        _investPanel = this.FindControl<Panel>("InvestPagePanel");
        _projectListScrollable = this.FindControl<ScrollableView>("ProjectListPanel");

        // Wire refresh button
        var refreshBtn = this.FindControl<Button>("RefreshProjectsButton");
        if (refreshBtn != null)
        {
            refreshBtn.Click += async (_, _) =>
            {
                if (DataContext is FindProjectsViewModel fvm)
                    await fvm.LoadProjectsFromSdkAsync();
            };
        }

        // Wire "Load More" button — explicit reveal only, no scroll trigger.
        // The VM's IsLoadingMore flag disables the button across the batch
        // insert so repeated taps can't pile up concurrent layout passes.
        var loadMoreBtn = this.FindControl<Button>("LoadMoreButton");
        if (loadMoreBtn != null)
        {
            loadMoreBtn.Click += (_, _) =>
            {
                if (DataContext is FindProjectsViewModel fvm)
                    fvm.LoadMore();
            };
        }

        // Listen for taps on ProjectCard elements to open project detail
        AddHandler(InputElement.TappedEvent, OnCardTapped, RoutingStrategies.Bubble);
        var findMs = sw.ElapsedMilliseconds;

        sw.Restart();
        // Manage visibility of the project list panel based on ViewModel state
        DataContextChanged += (_, _) => SubscribeToVisibility();
        SubscribeToVisibility();
        var subMs = sw.ElapsedMilliseconds;

        sw.Restart();
        // ── Responsive layout: adjust bottom padding for tab bar clearance ──
        _layoutSubscription = LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact => ApplyResponsiveLayout(isCompact));
        var layoutMs = sw.ElapsedMilliseconds;

        App.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("FindProjectsPerf")
            .LogInformation(
                "[FindProjectsView.ctor] init={Init}ms dc={Dc}ms find={Find}ms sub={Sub}ms layout={Layout}ms",
                initMs, dcMs, findMs, subMs, layoutMs);
    }

    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_projectListScrollable != null)
            _projectListScrollable.ContentPadding = isCompact
                ? new Thickness(16, 16, 16, 96)
                : new Thickness(24);
    }

    private void SubscribeToVisibility()
    {
        _visibilitySubscription?.Dispose();

        if (DataContext is FindProjectsViewModel vm)
        {
            // Manage 3 mutually exclusive panels:
            // - ProjectListPanel: visible when no selection at all
            // - ProjectDetailPanel: visible when project selected but invest page not open
            // - InvestPagePanel: visible when invest page is open
            _visibilitySubscription = vm.WhenAnyValue(x => x.SelectedProject, x => x.InvestPageViewModel)
              .Subscribe(tuple =>
              {
                  var hasProject = tuple.Item1 != null;
                  var hasInvest = tuple.Item2 != null;

                  if (ProjectListPanel != null)
                      ProjectListPanel.IsVisible = !hasProject && !hasInvest;

                  if (_detailPanel != null)
                  {
                      if (hasProject && !hasInvest)
                      {
                          if (_projectDetailView == null)
                          {
                              var dtSw = System.Diagnostics.Stopwatch.StartNew();
                              _projectDetailView = new ProjectDetailView { DataContext = tuple.Item1 };
                              _detailPanel.Children.Add(_projectDetailView);
                              dtSw.Stop();
                              global::App.App.Services.GetRequiredService<ILoggerFactory>()
                                  .CreateLogger("ShellPerf")
                                  .LogInformation("[DrillDown] ProjectDetailView create+attach={Ms}ms", dtSw.ElapsedMilliseconds);
                          }
                          else
                          {
                              _projectDetailView.DataContext = tuple.Item1;
                          }
                      }
                      _detailPanel.IsVisible = hasProject && !hasInvest;
                  }

                  if (_investPanel != null)
                  {
                      if (hasInvest)
                      {
                          if (_investPageView == null)
                          {
                              var ipSw = System.Diagnostics.Stopwatch.StartNew();
                              _investPageView = new InvestPageView { DataContext = tuple.Item2 };
                              _investPanel.Children.Add(_investPageView);
                              ipSw.Stop();
                              global::App.App.Services.GetRequiredService<ILoggerFactory>()
                                  .CreateLogger("ShellPerf")
                                  .LogInformation("[DrillDown] InvestPageView create+attach={Ms}ms", ipSw.ElapsedMilliseconds);
                          }
                          else
                          {
                              _investPageView.DataContext = tuple.Item2;
                          }
                      }
                      _investPanel.IsVisible = hasInvest;
                  }

                  // Publish detail view state to ShellViewModel for mobile sub-tab/back-button visibility
                  var shell = this.FindAncestorOfType<ShellView>();
                  if (shell?.DataContext is ShellViewModel shellVm)
                  {
                      shellVm.IsProjectDetailOpen = hasProject && !hasInvest;
                      shellVm.IsInvestPageOpen = hasInvest;
                      if (hasProject && tuple.Item1 != null)
                          shellVm.ProjectDetailActionVerb = Shared.ProjectTypeTerminology.ActionVerb(
                              Shared.ProjectTypeExtensions.FromDisplayString(tuple.Item1.ProjectType));
                  }
              });
        }
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        // Re-subscribe if subscriptions were disposed (view re-attached from cache)
        if (_visibilitySubscription == null)
            SubscribeToVisibility();

        // Wire infinite scroll: hook the inner ScrollViewer and trigger LoadMore
        // when the user nears the bottom of the list. Done here (not in ctor)
        // because the template is applied lazily.
        WireInfiniteScroll();
    }

    private void WireInfiniteScroll()
    {
        if (_listScrollViewer != null) return;
        if (_projectListScrollable == null) return;

        _listScrollViewer = _projectListScrollable.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .FirstOrDefault();

        if (_listScrollViewer != null)
        {
            _listScrollViewer.ScrollChanged += OnListScrollChanged;
        }
    }

    private void OnListScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        if (DataContext is not FindProjectsViewModel vm) return;
        if (!vm.HasMoreItems) return;

        // Trigger LoadMore when the user scrolls within two viewport-heights of
        // the bottom. The VM's _loadMoreInFlight gate prevents re-entry while
        // inserts drain, so repeated ScrollChanged events during a flick don't
        // pile up concurrent layout invalidations.
        var distanceFromBottom = sv.Extent.Height - (sv.Offset.Y + sv.Viewport.Height);
        if (distanceFromBottom < sv.Viewport.Height * 2)
        {
            vm.LoadMore();
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _visibilitySubscription?.Dispose();
        _visibilitySubscription = null;
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        if (_listScrollViewer != null)
        {
            _listScrollViewer.ScrollChanged -= OnListScrollChanged;
            _listScrollViewer = null;
        }
        base.OnDetachedFromLogicalTree(e);
    }

    public void OnBecameActive()
    {
        if (DataContext is FindProjectsViewModel vm)
        {
            vm.CloseInvestPage();
            vm.CloseProjectDetail();
        }
    }

    public void OnBecameInactive() { }

    /// <summary>
    /// Mobile perf: pre-inflate ProjectDetailView and InvestPageView into their
    /// hidden host panels on ApplicationIdle so the first user drill-down from
    /// the project list is a fast DataContext swap (&lt;100ms) instead of a
    /// ~350ms XAML inflate. Called by ShellViewModel after tab pre-warm completes.
    /// Idempotent — safe to call multiple times.
    /// </summary>
    public void PreWarmDrillDownViews()
    {
        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS()) return;

        if (_projectDetailView == null && _detailPanel != null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _projectDetailView = new ProjectDetailView();
            _detailPanel.Children.Add(_projectDetailView);
            // Panel stays IsVisible=false — the view is in the tree but not shown.
            sw.Stop();
            global::App.App.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("ShellPerf")
                .LogInformation("[PreWarm] ProjectDetailView factoryMs={Ms}", sw.ElapsedMilliseconds);
        }

        if (_investPageView == null && _investPanel != null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _investPageView = new InvestPageView();
            _investPanel.Children.Add(_investPageView);
            sw.Stop();
            global::App.App.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("ShellPerf")
                .LogInformation("[PreWarm] InvestPageView factoryMs={Ms}", sw.ElapsedMilliseconds);
        }
    }

    private void OnCardTapped(object? sender, TappedEventArgs e)
    {
        // Walk up from tapped element to find the ProjectCard control
        var element = e.Source as Control;
        while (element != null && element is not ProjectCard)
            element = element.Parent as Control;

        if (element is ProjectCard card
            && card.DataContext is ProjectItemViewModel project
            && DataContext is FindProjectsViewModel vm)
        {
            vm.OpenProjectDetail(project);
            e.Handled = true;
        }
    }
}
