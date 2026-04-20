using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using App.UI.Shared;
using App.UI.Shared.Controls;
using App.UI.Shared.Helpers;
using App.UI.Shell;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace App.UI.Sections.Funders;

public partial class FundersView : UserControl
{
    private CompositeDisposable? _subscriptions;
    private IDisposable? _layoutSubscription;
    private ScrollableView? _fundersScrollable;

    /// <summary>Design-time only.</summary>
    public FundersView() => InitializeComponent();

    public FundersView(FundersViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // Wire up filter tab click handlers (Borders use Tapped, not Button.Click)
        if (TabWaiting != null)
            TabWaiting.Tapped += (_, _) => SetFilterFromTab("waiting");
        if (TabApproved != null)
            TabApproved.Tapped += (_, _) => SetFilterFromTab("approved");
        if (TabRejected != null)
            TabRejected.Tapped += (_, _) => SetFilterFromTab("rejected");

        // Subscribe to visibility states once DataContext is set
        DataContextChanged += (_, _) => SubscribeToVisibility();
        SubscribeToVisibility();

        // Cache ScrollableView for responsive bottom padding
        _fundersScrollable = this.FindControl<ScrollableView>("FundersListPanel");

        // ── Responsive layout: adjust bottom padding for tab bar clearance ──
        _layoutSubscription = LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact =>
            {
                if (_fundersScrollable != null)
                    _fundersScrollable.ContentPadding = isCompact
                        ? new Thickness(16, 16, 16, 96)
                        : new Thickness(24);
            });
    }

    private void SetFilterFromTab(string filter)
    {
        if (DataContext is FundersViewModel vm)
            vm.SetFilter(filter);
    }

    private void SubscribeToVisibility()
    {
        _subscriptions?.Dispose();

        if (DataContext is not FundersViewModel vm) return;

        _subscriptions = new CompositeDisposable();

        // Show/hide the empty filter panel and signatures list based on FilteredSignatures count
        var filterSub = vm.WhenAnyValue(x => x.FilteredSignatures)
          .Subscribe(filtered =>
          {
              var hasItems = filtered is { Count: > 0 };
              if (FilterEmptyPanel != null)
                  FilterEmptyPanel.IsVisible = !hasItems;
              if (SignaturesListPanel != null)
                  SignaturesListPanel.IsVisible = hasItems;
          });
        _subscriptions.Add(filterSub);

        // Update tab visual states when CurrentFilter changes
        var tabSub = vm.WhenAnyValue(x => x.CurrentFilter)
          .Subscribe(filter => UpdateTabVisuals(filter));
        _subscriptions.Add(tabSub);

        // Toggle spinning animation on refresh button icon
        var refreshSub = vm.WhenAnyValue(x => x.IsRefreshing)
          .Subscribe(isRefreshing =>
          {
              var refreshBtn = this.FindControl<Button>("RefreshButton");
              var icon = refreshBtn?.GetLogicalDescendants().OfType<Projektanker.Icons.Avalonia.Icon>().FirstOrDefault();
              icon?.Classes.Set("Spinning", isRefreshing);
          });
        _subscriptions.Add(refreshSub);

        _subscriptions.Add(Disposable.Create(() => vm.ToastRequested -= OnToastRequested));
        vm.ToastRequested += OnToastRequested;
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
        // (the subscriptions were disposed in OnDetachedFromLogicalTree).
        SubscribeToVisibility();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _subscriptions?.Dispose();
        _subscriptions = null;
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    /// <summary>
    /// Updates tab CSS classes to reflect the active filter.
    /// Active tab: FilterTabActive class (styles handle border + text color)
    /// Inactive tab: no FilterTabActive class
    /// </summary>
    private void UpdateTabVisuals(string activeFilter)
    {
        TabWaiting?.Classes.Set("FilterTabActive", activeFilter == "waiting");
        TabApproved?.Classes.Set("FilterTabActive", activeFilter == "approved");
        TabRejected?.Classes.Set("FilterTabActive", activeFilter == "rejected");
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn || DataContext is not FundersViewModel vm)
            return;

        switch (btn.Name)
        {
            case "ApproveAllButton":
                vm.ApproveAll();
                e.Handled = true;
                break;

            case "RefreshButton":
                _ = vm.RefreshAsync();
                e.Handled = true;
                break;

            case "ApproveButton" when btn.Tag is int approveId:
                vm.ApproveSignature(approveId);
                e.Handled = true;
                break;

            case "RejectButton" when btn.Tag is int rejectId:
                vm.RejectSignature(rejectId);
                e.Handled = true;
                break;

            case "ChatButton":
                // Chat action — placeholder for now
                e.Handled = true;
                break;

            case "ExpandButton" when btn.Tag is int expandId:
                vm.ToggleExpanded(expandId);
                ToggleExpandedPanel(expandId, vm.IsExpanded(expandId));
                e.Handled = true;
                break;

            case "CopyNpubButton" when btn.Tag is string npub:
                ClipboardHelper.CopyToClipboard(this, npub);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Find the ExpandedPanel with matching Tag and toggle its visibility.
    /// Also rotate the chevron icon on the ExpandButton.
    /// </summary>
    private void ToggleExpandedPanel(int id, bool isExpanded)
    {
        // Walk the visual tree of SignaturesListPanel to find matching panels
        if (SignaturesListPanel == null) return;

        foreach (var container in SignaturesListPanel.GetLogicalDescendants())
        {
            // Find ExpandedPanel borders with matching Tag
            if (container is Border { Name: "ExpandedPanel" } panel && panel.Tag is int panelId && panelId == id)
            {
                panel.IsVisible = isExpanded;
            }

            // Rotate the expand button chevron
            if (container is Button { Name: "ExpandButton" } expandBtn && expandBtn.Tag is int btnId && btnId == id)
            {
                expandBtn.RenderTransform = isExpanded
                    ? new RotateTransform(180)
                    : new RotateTransform(0);
            }
        }
    }
}
