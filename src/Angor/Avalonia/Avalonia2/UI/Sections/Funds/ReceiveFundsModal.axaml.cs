using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia2.UI.Shared.Helpers;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Sections.Funds;

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
    public void SetWallet(string name, string type)
    {
        ToWalletName.Text = name;
        ToWalletType.Text = type;
        AddressTypeLabel.Text = $"{type} Address";
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
                // Vue: copyAddress() — copies displayed address to clipboard
                ClipboardHelper.CopyToClipboard(this, AddressText.Text);
                break;
        }
    }
}
