using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using App.UI.Shared.Helpers;
using App.UI.Shell;

namespace App.UI.Sections.MyProjects.Deploy;

/// <summary>
/// Shell-level modal overlay for the Deploy flow.
/// Implements IBackdropCloseable so the shell can notify on backdrop clicks.
/// </summary>
public partial class DeployFlowOverlay : UserControl, IBackdropCloseable
{
    private Border? _selectedWalletBorder;

    /// <summary>
    /// Callback invoked when the user completes the deploy flow (success → "Go to My Projects").
    /// The parent (CreateProjectView) sets this to handle post-deploy navigation.
    /// </summary>
    public Action? OnDeployCompleted { get; set; }

    public DeployFlowOverlay()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick);
        // Handle wallet card clicks via PointerPressed on Border elements (no Button chrome)
        AddHandler(Border.PointerPressedEvent, OnWalletBorderPressed, RoutingStrategies.Bubble);
    }

    private DeployFlowViewModel? Vm => DataContext as DeployFlowViewModel;

    private ShellViewModel? GetShellVm()
    {
        // Walk up the visual tree to find ShellView → ShellViewModel
        var shellView = this.FindAncestorOfType<ShellView>();
        return shellView?.DataContext as ShellViewModel;
    }

    /// <summary>
    /// Called by the shell when the backdrop is clicked.
    /// Handles cleanup logic (resetting VM state) before the shell closes the modal.
    /// </summary>
    public void OnBackdropCloseRequested()
    {
        if (Vm?.IsSuccess == true)
        {
            Vm.GoToMyProjects();
            OnDeployCompleted?.Invoke();
        }
        else
        {
            Vm?.Close();
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            // Wallet selector
            case "CloseWalletSelector":
                // Vue ref: X button / backdrop click → showDeployWalletModal = false (returns to step 6)
                Vm?.Close();
                GetShellVm()?.HideModal();
                break;
            case "PayWithWalletButton":
                // Vue ref: payWithDeployWallet() → shows QR with "received" → success
                Vm?.PayWithWallet();
                break;
            case "PayInvoiceInsteadButton":
                // Vue ref: proceedToDeployInvoice() → QR code modal
                Vm?.ShowPayFee();
                break;

            // Pay fee
            case "ClosePayFee":
                // Vue ref: closeDeployModal() → resets all state, back to step 6
                Vm?.Close();
                GetShellVm()?.HideModal();
                break;
            case "BackToWalletsButton":
                Vm?.BackToWalletSelector();
                break;
            case "CopyInvoiceButton":
                ClipboardHelper.CopyToClipboard(this, Vm?.InvoiceString);
                break;

            // Success — Vue ref: no X button on success modal
            case "GoToMyProjectsButton":
                // Vue ref: goToMyProjects() — creates project, adds to list, closes wizard
                Vm?.GoToMyProjects();
                GetShellVm()?.HideModal();
                OnDeployCompleted?.Invoke();
                break;
            case "CompleteProfileButton":
                // Vue ref: completeProfile() opens /profile/{id} in new tab, does NOT close modal.
                // Desktop stub: just go to my projects (same as "Go to My Projects").
                Vm?.GoToMyProjects();
                GetShellVm()?.HideModal();
                OnDeployCompleted?.Invoke();
                break;
        }
    }

    /// <summary>
    /// Handle clicks on WalletBorder elements (replaces Button to avoid FluentTheme hover chrome).
    /// Walks up from the clicked element to find the WalletBorder, then uses its DataContext.
    /// </summary>
    private void OnWalletBorderPressed(object? sender, PointerPressedEventArgs e)
    {
        // Walk up from the hit target to find a Border named "WalletBorder"
        var source = e.Source as Control;
        Border? walletBorder = null;
        while (source != null)
        {
            if (source is Border b && b.Name == "WalletBorder")
            {
                walletBorder = b;
                break;
            }
            source = source.Parent as Control;
        }

        if (walletBorder?.DataContext is WalletItem wallet)
        {
            Vm?.SelectWallet(wallet);
            _selectedWalletBorder = WalletSelectionHelper.UpdateWalletSelection(_selectedWalletBorder, walletBorder);
            e.Handled = true;
        }
    }
}
