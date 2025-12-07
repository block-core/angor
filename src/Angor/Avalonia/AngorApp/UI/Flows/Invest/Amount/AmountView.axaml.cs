using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AngorApp.UI.Flows.Invest.Amount;

public partial class AmountView : UserControl
{
    public AmountView()
    {
        InitializeComponent();

        // Wire up the pattern selection change handler
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Find the ListBox and hook up the SelectionChanged event
        var listBox = this.FindControl<ListBox>("PatternListBox");
        if (listBox != null)
        {
            listBox.SelectionChanged += PatternListBox_SelectionChanged;

            // Set initial selection to first item if RequiresPatternSelection
            if (DataContext is IAmountViewModel viewModel && viewModel.RequiresPatternSelection)
            {
                listBox.SelectedIndex = 0;
                viewModel.SelectedPatternIndex = 0;
            }
        }
    }

    private void PatternListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is IAmountViewModel viewModel && sender is ListBox listBox)
        {
            // Convert SelectedIndex to byte? and update the ViewModel
            if (listBox.SelectedIndex >= 0 && listBox.SelectedIndex <= 255)
            {
                viewModel.SelectedPatternIndex = (byte)listBox.SelectedIndex;
            }
            else
            {
                viewModel.SelectedPatternIndex = null;
            }
        }
    }
}