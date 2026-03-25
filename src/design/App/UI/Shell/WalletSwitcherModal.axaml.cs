using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace App.UI.Shell;

/// <summary>
/// Shell-level wallet switcher modal.
/// DataContext = ShellViewModel (set by the ShellView when opening).
/// Implements IBackdropCloseable so clicking the backdrop closes the modal.
/// Vue: "Select Wallet" modal in App.vue — clicking a wallet selects it and closes.
/// </summary>
public partial class WalletSwitcherModal : UserControl, IBackdropCloseable
{
    private Border? _selectedWalletBorder;

    public WalletSwitcherModal()
    {
        InitializeComponent();

        AddHandler(Button.ClickEvent, OnButtonClick);
        AddHandler(Border.PointerPressedEvent, OnBorderPressed, RoutingStrategies.Bubble);
    }

    private ShellViewModel? Vm => DataContext as ShellViewModel;

    /// <summary>
    /// Called by the shell when the backdrop is clicked — just close.
    /// </summary>
    public void OnBackdropCloseRequested()
    {
        Vm?.HideModal();
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "CloseWalletSwitcher":
                Vm?.HideModal();
                break;
        }
    }

    private void OnBorderPressed(object? sender, PointerPressedEventArgs e)
    {
        var source = e.Source as Control;
        Border? found = null;

        while (source != null)
        {
            if (source is Border b && b.Name == "WalletBorder")
            {
                found = b;
                break;
            }
            source = source.Parent as Control;
        }

        if (found?.DataContext is WalletSwitcherItem wallet)
        {
            // Select the wallet
            Vm?.SelectSwitcherWallet(wallet);

            // Update visual states: deselect previous, select new (no tree walk)
            _selectedWalletBorder?.Classes.Set("WalletSelected", false);
            found.Classes.Set("WalletSelected", true);
            _selectedWalletBorder = found;

            // Vue behavior: selecting a wallet immediately closes the modal
            Vm?.HideModal();

            e.Handled = true;
        }
    }
}
