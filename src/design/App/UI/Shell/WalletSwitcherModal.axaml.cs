using Avalonia.Controls;
using Avalonia.Interactivity;
using App.UI.Shared.Services;

namespace App.UI.Shell;

/// <summary>
/// Shell-level wallet switcher modal.
/// DataContext = ShellViewModel (set by the ShellView when opening).
/// Implements IBackdropCloseable so clicking the backdrop closes the modal.
/// Vue: "Select Wallet" modal in App.vue — clicking a wallet selects it and closes.
/// </summary>
public partial class WalletSwitcherModal : UserControl, IBackdropCloseable
{
    private Button? _selectedWalletButton;

    public WalletSwitcherModal()
    {
        InitializeComponent();

        AddHandler(Button.ClickEvent, OnButtonClick);
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

            case "WalletButton":
                if (btn.CommandParameter is WalletInfo wallet)
                {
                    Vm?.SelectSwitcherWallet(wallet);

                    _selectedWalletButton?.Classes.Set("WalletSelected", false);
                    btn.Classes.Set("WalletSelected", true);
                    _selectedWalletButton = btn;

                    Vm?.HideModal();
                }
                break;
        }
    }
}
