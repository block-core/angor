using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia2.UI.Shared;
using Avalonia2.UI.Shared.Controls;
using Avalonia2.UI.Shell;
using System.Reactive.Linq;

namespace Avalonia2.UI.Sections.FindProjects;

public partial class FindProjectsView : UserControl
{
    private IDisposable? _visibilitySubscription;
    private IDisposable? _layoutSubscription;

    // Cached FindControl results — avoid repeated tree walks on every visibility update
    private Panel? _detailPanel;
    private Panel? _investPanel;
    private ScrollableView? _projectListScrollable;

    /// <summary>Design-time only.</summary>
    public FindProjectsView() => InitializeComponent();

    public FindProjectsView(FindProjectsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // Cache panels once
        _detailPanel = this.FindControl<Panel>("ProjectDetailPanel");
        _investPanel = this.FindControl<Panel>("InvestPagePanel");
        _projectListScrollable = this.FindControl<ScrollableView>("ProjectListPanel");

        // Listen for taps on ProjectCard elements to open project detail
        AddHandler(InputElement.TappedEvent, OnCardTapped, RoutingStrategies.Bubble);

        // Manage visibility of the project list panel based on ViewModel state
        DataContextChanged += (_, _) => SubscribeToVisibility();
        SubscribeToVisibility();

        // ── Responsive layout: adjust bottom padding for tab bar clearance ──
        _layoutSubscription = LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact => ApplyResponsiveLayout(isCompact));
    }

    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_projectListScrollable != null)
            _projectListScrollable.ContentPadding = isCompact
                ? new Thickness(24, 24, 24, 96)
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
                      _detailPanel.IsVisible = hasProject && !hasInvest;

                  if (_investPanel != null)
                      _investPanel.IsVisible = hasInvest;

                  // Publish detail view state to ShellViewModel for mobile sub-tab/back-button visibility
                  var shell = this.FindAncestorOfType<ShellView>();
                  if (shell?.DataContext is ShellViewModel shellVm)
                  {
                      shellVm.IsProjectDetailOpen = hasProject && !hasInvest;
                      shellVm.IsInvestPageOpen = hasInvest;
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
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _visibilitySubscription?.Dispose();
        _visibilitySubscription = null;
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
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
