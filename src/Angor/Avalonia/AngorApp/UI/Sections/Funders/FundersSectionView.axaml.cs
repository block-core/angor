using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AngorApp.UI.Sections.Funders;

public partial class FundersSectionView : UserControl
{
    public FundersSectionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IFundersSectionViewModel vm)
        {
            vm.Refresh.Execute(null);
        }
    }
}
