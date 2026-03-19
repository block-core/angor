using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia2.UI.Shell;
using Avalonia2.UI.Shared;
using Avalonia.VisualTree;
using ReactiveUI;

namespace Avalonia2.UI.Sections.Home;

public partial class HomeView : UserControl
{
    /// <summary>Design-time only.</summary>
    public HomeView() => InitializeComponent();

    public HomeView(HomeViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // ── Responsive layout: two-col (desktop) → stacked (compact) ──
        var homeGrid = this.FindControl<Grid>("HomeGrid")!;
        var fundCard = this.FindControl<Grid>("FundCard")!;
        var getFundedCard = this.FindControl<Grid>("GetFundedCard")!;

        LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact =>
            {
                if (isCompact)
                {
                    // Stacked: single column, two rows with 24px gap
                    homeGrid.ColumnDefinitions.Clear();
                    homeGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    homeGrid.RowDefinitions.Clear();
                    homeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    homeGrid.RowDefinitions.Add(new RowDefinition(24, GridUnitType.Pixel));
                    homeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                    Grid.SetColumn(fundCard, 0);
                    Grid.SetRow(fundCard, 0);
                    Grid.SetColumn(getFundedCard, 0);
                    Grid.SetRow(getFundedCard, 2);
                }
                else
                {
                    // Side by side: two columns with 24px gap column
                    homeGrid.ColumnDefinitions.Clear();
                    homeGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    homeGrid.ColumnDefinitions.Add(new ColumnDefinition(24, GridUnitType.Pixel));
                    homeGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    homeGrid.RowDefinitions.Clear();
                    homeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

                    Grid.SetColumn(fundCard, 0);
                    Grid.SetRow(fundCard, 0);
                    Grid.SetColumn(getFundedCard, 2);
                    Grid.SetRow(getFundedCard, 0);
                }
            });
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
