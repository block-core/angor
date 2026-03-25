using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia2.UI.Shared.Helpers;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Shared.Controls;

/// <summary>
/// Private Keys Password Step — thin code-behind that delegates to PrivateKeysPasswordModalViewModel.
/// Only retains modal flow logic (close → open PrivateKeysDisplayModal).
/// Vue ref: ManageFunds.vue lines 559-611.
/// </summary>
public partial class PrivateKeysPasswordModal : UserControl, IBackdropCloseable
{
    private readonly PrivateKeysPasswordModalViewModel _vm;

    /// <summary>Parameterless ctor for XAML designer.</summary>
    public PrivateKeysPasswordModal() : this("", "", "", "", "", "", "") { }

    public PrivateKeysPasswordModal(
        string projectId, string founderKey, string recoveryKey,
        string nostrNpub, string nip05, string nostrNsec, string nostrHex)
    {
        _vm = new PrivateKeysPasswordModalViewModel(
            projectId, founderKey, recoveryKey,
            nostrNpub, nip05, nostrNsec, nostrHex);
        DataContext = _vm;

        InitializeComponent();

        CloseButton.Click += OnCloseClick;
        CancelButton.Click += OnCloseClick;
        ViewKeysButton.Click += OnViewKeysClick;
    }

    public void OnBackdropCloseRequested() { }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        ShellService.HideModal();
    }

    private void OnViewKeysClick(object? sender, RoutedEventArgs e)
    {
        if (!_vm.ValidateAndViewKeys()) return;

        // Close password modal and open keys display modal
        ShellService.HideModal();

        // Use Dispatcher to allow the close animation to start before opening next modal
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var keysModal = new PrivateKeysDisplayModal(
                _vm.ProjectId, _vm.FounderKey, _vm.RecoveryKey,
                _vm.NostrNpub, _vm.Nip05, _vm.NostrNsec, _vm.NostrHex);
            ShellService.ShowModal(keysModal);
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
}
