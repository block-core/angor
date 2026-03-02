using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia2.UI.Sections.MyProjects.Deploy;
using Avalonia2.UI.Shared.Helpers;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Sections.FindProjects;

/// <summary>
/// Shell-level modal overlay for the Invest flow.
/// Contains Wallet Selector, Invoice/QR, and Success modals.
/// DataContext = InvestPageViewModel.
/// Implements IBackdropCloseable so the shell can notify on backdrop clicks.
/// </summary>
public partial class InvestModalsView : UserControl, IBackdropCloseable
{
    private Border? _selectedWalletBorder;

    /// <summary>
    /// Callback invoked when the user completes the flow (success → "View My Investments").
    /// The parent (InvestPageView) sets this to navigate back to the project list.
    /// </summary>
    public Action? OnNavigateBackToList { get; set; }

    public InvestModalsView()
    {
        InitializeComponent();

        AddHandler(Button.ClickEvent, OnButtonClick);
        AddHandler(Border.PointerPressedEvent, OnBorderPressed, RoutingStrategies.Bubble);
    }

    private InvestPageViewModel? Vm => DataContext as InvestPageViewModel;

    /// <summary>
    /// Called by the shell when the backdrop is clicked.
    /// Handles cleanup logic (resetting VM state) before the shell closes the modal.
    /// </summary>
    public void OnBackdropCloseRequested()
    {
        if (Vm?.IsSuccess == true)
        {
            OnNavigateBackToList?.Invoke();
        }
        else
        {
            Vm?.CloseModal();
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "CloseWalletSelector":
                Vm?.CloseModal();
                GetShellVm()?.HideModal();
                break;

            case "PayWithWalletButton":
                Vm?.PayWithWallet();
                break;

            case "PayInvoiceInsteadButton":
                Vm?.ShowInvoice();
                break;

            case "CloseInvoice":
                Vm?.CloseModal();
                GetShellVm()?.HideModal();
                break;

            case "CopyInvoiceButton":
                ClipboardHelper.CopyToClipboard(this, Vm?.InvoiceString);
                break;

            case "ViewInvestmentsButton":
                GetShellVm()?.HideModal();
                OnNavigateBackToList?.Invoke();
                break;
        }
    }

    private ShellViewModel? GetShellVm()
    {
        var shellView = this.FindAncestorOfType<ShellView>();
        return shellView?.DataContext as ShellViewModel;
    }

    private void OnBorderPressed(object? sender, PointerPressedEventArgs e)
    {
        var source = e.Source as Control;
        Border? found = null;
        string? foundName = null;

        while (source != null)
        {
            if (source is Border b && !string.IsNullOrEmpty(b.Name))
            {
                var name = b.Name;
                if (name == "WalletBorder" || name == "QrCodePlaceholder")
                {
                    found = b;
                    foundName = name;
                    break;
                }
            }
            source = source.Parent as Control;
        }

        if (found == null || foundName == null) return;

        switch (foundName)
        {
            case "WalletBorder":
                if (found.DataContext is WalletItem wallet)
                {
                    Vm?.SelectWallet(wallet);
                    _selectedWalletBorder = WalletSelectionHelper.UpdateWalletSelection(_selectedWalletBorder, found);
                    e.Handled = true;
                }
                break;

            case "QrCodePlaceholder":
                Vm?.PayViaInvoice();
                e.Handled = true;
                break;
        }
    }
}

