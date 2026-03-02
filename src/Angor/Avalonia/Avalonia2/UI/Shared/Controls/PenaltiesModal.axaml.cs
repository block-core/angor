using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Shared.Controls;

/// <summary>
/// Penalties Details Modal — Vue InvestmentDetail.vue lines 563-626
/// Shows a table of penalty entries with project ID, amount, and days remaining.
/// Opened from PortfolioView Penalties button via shell modal pattern.
/// </summary>
public partial class PenaltiesModal : UserControl, IBackdropCloseable
{
    public PenaltiesModal()
    {
        InitializeComponent();

        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += OnCloseClick;
    }

    private ShellViewModel? ShellVm =>
        this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;

    public void OnBackdropCloseRequested()
    {
        ShellVm?.HideModal();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        ShellVm?.HideModal();
    }
}
