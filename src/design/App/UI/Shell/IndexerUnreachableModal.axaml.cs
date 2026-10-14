using Avalonia.Controls;
using Avalonia.Interactivity;

namespace App.UI.Shell;

/// <summary>
/// Code-behind for the shell-level "indexer unreachable" popup.
/// DataContext = IndexerUnreachableModalViewModel (set when the modal is constructed).
/// </summary>
public partial class IndexerUnreachableModal : UserControl, IBackdropCloseable
{
    public IndexerUnreachableModal()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick);
    }

    private IndexerUnreachableModalViewModel? Vm => DataContext as IndexerUnreachableModalViewModel;

    /// <summary>
    /// Backdrop click just closes the popup without picking an indexer — the caller
    /// is still responsible for retrying whatever operation failed.
    /// </summary>
    public void OnBackdropCloseRequested() => Vm?.Close();

    private async void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "IndexerButton":
                if (btn.CommandParameter is IndexerPickerItem item)
                {
                    Vm?.SelectIndexer(item);
                }
                break;

            case "AddCustomIndexerButton":
                if (Vm != null)
                {
                    await Vm.AddAndSelectCustomIndexerAsync();
                }
                break;
        }
    }
}
