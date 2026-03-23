using Avalonia.Input;
using Avalonia.Interactivity;
using AngorApp.Model.Common;

namespace AngorApp.UI.Shared.Controls.Common;

public partial class LongTextView : UserControl
{
    public LongTextView()
    {
        InitializeComponent();
    }

    private async void CopyToClipboard_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LongTextViewModel vm && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(vm.Text);
        }
    }
}
