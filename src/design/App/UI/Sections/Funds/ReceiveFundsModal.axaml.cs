using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using App.UI.Shared.Helpers;
using App.UI.Shell;

namespace App.UI.Sections.Funds;

/// <summary>
/// Receive Funds Modal — Vue Funds.vue receive flow:
///   Single step: To wallet info → QR code → copyable address → Done
///
/// DataContext = FundsViewModel.
/// The wallet name/type are set via SetWallet() before showing.
/// </summary>
public partial class ReceiveFundsModal : UserControl, IBackdropCloseable
{
    public ReceiveFundsModal()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick);
    }

    private ShellViewModel? ShellVm =>
        this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;

    public void OnBackdropCloseRequested() { }

    /// <summary>
    /// Set the destination wallet info shown in the "To" box.
    /// Called by FundsView before showing the modal.
    /// </summary>
    public void SetWallet(string name, string type, string? walletId = null)
    {
        ToWalletName.Text = name;
        ToWalletType.Text = type;
        AddressTypeLabel.Text = $"{type} Address";

        if (!string.IsNullOrEmpty(walletId) && DataContext is FundsViewModel fundsVm)
        {
            _ = LoadReceiveAddressAsync(fundsVm, walletId);
        }
    }

    private async Task LoadReceiveAddressAsync(FundsViewModel fundsVm, string walletId)
    {
        var address = await fundsVm.GetReceiveAddressAsync(walletId);
        if (!string.IsNullOrEmpty(address))
        {
            AddressText.Text = address;
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "CloseBtn":
            case "BtnDone":
                ShellVm?.HideModal();
                break;

            case "BtnCopyAddress":
                ClipboardHelper.CopyToClipboard(this, AddressText.Text);
                break;
        }
    }
}
