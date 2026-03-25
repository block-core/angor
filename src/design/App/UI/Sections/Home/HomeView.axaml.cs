using Avalonia.Controls;
using Avalonia.Interactivity;
using App.UI.Shell;
using Avalonia.VisualTree;

namespace App.UI.Sections.Home;

public partial class HomeView : UserControl
{
    /// <summary>Design-time only.</summary>
    public HomeView() => InitializeComponent();

    public HomeView(HomeViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
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
