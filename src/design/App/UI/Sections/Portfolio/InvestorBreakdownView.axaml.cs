using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using App.UI.Shared;
using App.UI.Shell;

namespace App.UI.Sections.Portfolio;

public partial class InvestorBreakdownView : UserControl
{
    private StackPanel? _tableDesktop;
    private ItemsControl? _cardsMobile;
    private Grid? _summaryStatsGrid;
    private Border? _statCardInvestors;
    private IDisposable? _layoutSubscription;

    public InvestorBreakdownView()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
        SubscribeToLayoutMode();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _tableDesktop = this.FindControl<StackPanel>("BreakdownTableDesktop");
        _cardsMobile = this.FindControl<ItemsControl>("BreakdownCardsMobile");
        _summaryStatsGrid = this.FindControl<Grid>("SummaryStatsGrid");
        _statCardInvestors = this.FindControl<Border>("StatCardInvestors");
        ApplyResponsiveLayout(LayoutModeService.Instance.IsCompact);
    }

    /// <summary>Idempotent responsive-layout subscription — re-created on every logical-tree attach because OnDetachedFromLogicalTree disposes it.</summary>
    private void SubscribeToLayoutMode()
    {
        if (_layoutSubscription != null) return;
        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        SubscribeToLayoutMode();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    /// <summary>
    /// Compact: fixed-width table → stacked cards (house pattern, same as
    /// InvestmentDetailView stages), and the two summary stat cards stack.
    /// </summary>
    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_tableDesktop != null) _tableDesktop.IsVisible = !isCompact;
        if (_cardsMobile != null) _cardsMobile.IsVisible = isCompact;

        if (_summaryStatsGrid == null || _statCardInvestors == null) return;
        if (isCompact)
        {
            _summaryStatsGrid.ColumnDefinitions[1].Width = new GridLength(0);
            _summaryStatsGrid.ColumnDefinitions[2].Width = new GridLength(0);
            Grid.SetColumn(_statCardInvestors, 0);
            Grid.SetRow(_statCardInvestors, 1);
            _statCardInvestors.Margin = new Thickness(0, 12, 0, 0);
        }
        else
        {
            _summaryStatsGrid.ColumnDefinitions[1].Width = new GridLength(16);
            _summaryStatsGrid.ColumnDefinitions[2].Width = GridLength.Star;
            Grid.SetColumn(_statCardInvestors, 2);
            Grid.SetRow(_statCardInvestors, 0);
            _statCardInvestors.Margin = new Thickness(0);
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { Name: "CloseButton" or "CloseButtonX" })
        {
            var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
            shellVm?.HideModal();
        }
    }
}
