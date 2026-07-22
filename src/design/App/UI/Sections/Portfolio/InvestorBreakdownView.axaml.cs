using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using App.UI.Shell;

namespace App.UI.Sections.Portfolio;

public partial class InvestorBreakdownView : UserControl
{
    public InvestorBreakdownView()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { Name: "CloseButton" })
        {
            var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
            shellVm?.HideModal();
        }
    }
}
