using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia2.UI.Shared;
using Avalonia2.UI.Shared.Controls;
using Avalonia2.UI.Shell;
using System.Reactive.Linq;

namespace Avalonia2.UI.Sections.Portfolio;

public partial class PortfolioView : UserControl
{
    private IDisposable? _visibilitySubscription;
    private IDisposable? _layoutSubscription;

    /// <summary>Design-time only.</summary>
    public PortfolioView() => InitializeComponent();

    public PortfolioView(PortfolioViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // When navigating back to Funded, clear any open detail view
        // so the user sees the list (not a stale detail screen from last time).
        vm.CloseInvestmentDetail();

        // Manage visibility of the portfolio list panel based on ViewModel state
        DataContextChanged += (_, _) => SubscribeToVisibility();
        SubscribeToVisibility();

        // Wire Penalties button to open shell modal
        var penaltiesBtn = this.FindControl<Button>("PenaltiesButton");
        if (penaltiesBtn != null) penaltiesBtn.Click += OnPenaltiesClick;

        // ── Responsive layout: 380px sidebar + content (desktop) → stacked (compact) ──
        var portfolioGrid = this.FindControl<Grid>("PortfolioListPanel")!;
        var sidebar = this.FindControl<Border>("PortfolioSidebar")!;
        var content = this.FindControl<ScrollableView>("PortfolioContent")!;

        _layoutSubscription = LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact =>
            {
                if (isCompact)
                {
                    // Stacked: single column, sidebar above content
                    portfolioGrid.ColumnDefinitions.Clear();
                    portfolioGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    portfolioGrid.RowDefinitions.Clear();
                    portfolioGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    portfolioGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

                    Grid.SetColumn(sidebar, 0);
                    Grid.SetRow(sidebar, 0);
                    sidebar.Margin = new Avalonia.Thickness(0, 0, 0, 24);
                    sidebar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

                    Grid.SetColumn(content, 0);
                    Grid.SetRow(content, 1);
                    content.ContentPadding = new Avalonia.Thickness(0);
                }
                else
                {
                    // Side by side: 380px sidebar + * content
                    portfolioGrid.ColumnDefinitions.Clear();
                    portfolioGrid.ColumnDefinitions.Add(new ColumnDefinition(380, GridUnitType.Pixel));
                    portfolioGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    portfolioGrid.RowDefinitions.Clear();
                    portfolioGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

                    Grid.SetColumn(sidebar, 0);
                    Grid.SetRow(sidebar, 0);
                    sidebar.Margin = new Avalonia.Thickness(0, 0, 24, 0);
                    sidebar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;

                    Grid.SetColumn(content, 1);
                    Grid.SetRow(content, 0);
                    content.ContentPadding = new Avalonia.Thickness(0, 0, 16, 0);
                }
            });
    }

    private void SubscribeToVisibility()
    {
        _visibilitySubscription?.Dispose();

        if (DataContext is PortfolioViewModel vm)
        {
            // Portfolio list is visible when: HasInvestments AND no detail selected.
            // HasInvestments is handled by XAML binding; here we also hide when
            // SelectedInvestment is set (drill-down to detail view).
            _visibilitySubscription = vm.WhenAnyValue(
                x => x.HasInvestments,
                x => x.SelectedInvestment,
                (hasInvestments, selected) => hasInvestments && selected == null)
              .Subscribe(visible =>
              {
                  if (PortfolioListPanel != null)
                      PortfolioListPanel.IsVisible = visible;
              });
        }
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        // Re-subscribe when the cached view is re-added to the tree
        // (the subscription was disposed in OnDetachedFromLogicalTree).
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

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button btn && btn.Name == "ManageButton" &&
            btn.Tag is InvestmentViewModel investment &&
            DataContext is PortfolioViewModel vm)
        {
            vm.OpenInvestmentDetail(investment);
        }
    }

    private void OnPenaltiesClick(object? sender, RoutedEventArgs e)
    {
        var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
        shellVm?.ShowModal(new PenaltiesModal());
    }
}
