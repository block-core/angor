using Avalonia.Controls;
using Avalonia.Interactivity;

namespace App.UI.Sections.MyProjects.Steps;

public partial class CreateProjectStep2View : UserControl
{
    public CreateProjectStep2View()
    {
        InitializeComponent();
    }

    private void OnDebugPrefillClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CreateProjectViewModel vm)
        {
            vm.PrepopulateDebugData();
        }
    }
}
