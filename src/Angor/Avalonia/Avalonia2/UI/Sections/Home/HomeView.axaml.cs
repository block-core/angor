using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia2.UI.Shell;
using Avalonia2.UI.Shared;
using Avalonia.VisualTree;
using ReactiveUI;

namespace Avalonia2.UI.Sections.Home;

public partial class HomeView : UserControl
{
    private IDisposable? _layoutSubscription;

    // Cached controls for responsive layout
    private Grid? _homeGrid;
    private Grid? _fundCard;
    private Grid? _getFundedCard;

    /// <summary>Design-time only.</summary>
    public HomeView() => InitializeComponent();

    public HomeView(HomeViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // Cache responsive layout controls
        _homeGrid = this.FindControl<Grid>("HomeGrid");
        _fundCard = this.FindControl<Grid>("FundCard");
        _getFundedCard = this.FindControl<Grid>("GetFundedCard");

        // ── Responsive layout: two-col (desktop) → stacked (compact) ──
        _layoutSubscription = LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact => ApplyResponsiveLayout(isCompact));
    }

    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_homeGrid == null || _fundCard == null || _getFundedCard == null) return;

        if (isCompact)
        {
            // Stacked: single column, two rows with 24px gap
            _homeGrid.ColumnDefinitions.Clear();
            _homeGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _homeGrid.RowDefinitions.Clear();
            _homeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _homeGrid.RowDefinitions.Add(new RowDefinition(24, GridUnitType.Pixel));
            _homeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(_fundCard, 0);
            Grid.SetRow(_fundCard, 0);
            Grid.SetColumn(_getFundedCard, 0);
            Grid.SetRow(_getFundedCard, 2);
        }
        else
        {
            // Side by side: two columns with 24px gap column
            _homeGrid.ColumnDefinitions.Clear();
            _homeGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _homeGrid.ColumnDefinitions.Add(new ColumnDefinition(24, GridUnitType.Pixel));
            _homeGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _homeGrid.RowDefinitions.Clear();
            _homeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

            Grid.SetColumn(_fundCard, 0);
            Grid.SetRow(_fundCard, 0);
            Grid.SetColumn(_getFundedCard, 2);
            Grid.SetRow(_getFundedCard, 0);
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        // Find the ShellViewModel via visual tree
        var shell = this.FindAncestorOfType<ShellView>();
        var shellVm = shell?.DataContext as ShellViewModel;
        if (shellVm == null) return;

        // Match by button content text
        var text = GetButtonText(btn);
        switch (text)
        {
            case "Launch a Project":
                shellVm.NavigateToMyProjectsAndLaunch();
                break;
            case "Find Projects":
                shellVm.NavigateToFindProjects();
                break;
        }
    }

    private static string? GetButtonText(Button btn)
    {
        if (btn.Content is string s) return s;
        return null;
    }
}
