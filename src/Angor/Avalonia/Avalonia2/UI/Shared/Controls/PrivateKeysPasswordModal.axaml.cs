using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Shared.Controls;

/// <summary>
/// Private Keys Password Step — shell modal with blur backdrop.
/// Vue ref: ManageFunds.vue lines 559-611.
/// On successful password entry, opens PrivateKeysDisplayModal.
/// </summary>
public partial class PrivateKeysPasswordModal : UserControl, IBackdropCloseable
{
    private readonly string _projectId;
    private readonly string _founderKey;
    private readonly string _recoveryKey;
    private readonly string _nostrNpub;
    private readonly string _nip05;
    private readonly string _nostrNsec;
    private readonly string _nostrHex;

    /// <summary>Parameterless ctor for XAML designer.</summary>
    public PrivateKeysPasswordModal() : this("", "", "", "", "", "", "") { }

    public PrivateKeysPasswordModal(
        string projectId, string founderKey, string recoveryKey,
        string nostrNpub, string nip05, string nostrNsec, string nostrHex)
    {
        _projectId = projectId;
        _founderKey = founderKey;
        _recoveryKey = recoveryKey;
        _nostrNpub = nostrNpub;
        _nip05 = nip05;
        _nostrNsec = nostrNsec;
        _nostrHex = nostrHex;

        InitializeComponent();

        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += OnCloseClick;

        var cancelBtn = this.FindControl<Button>("CancelButton");
        if (cancelBtn != null) cancelBtn.Click += OnCloseClick;

        var viewKeysBtn = this.FindControl<Button>("ViewKeysButton");
        if (viewKeysBtn != null) viewKeysBtn.Click += OnViewKeysClick;
    }

    private ShellViewModel? ShellVm =>
        this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;

    public void OnBackdropCloseRequested() { }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        ShellVm?.HideModal();
    }

    private void OnViewKeysClick(object? sender, RoutedEventArgs e)
    {
        var passwordInput = this.FindControl<TextBox>("PasswordInput");
        var password = passwordInput?.Text;
        if (string.IsNullOrWhiteSpace(password)) return;

        var shellVm = ShellVm;
        if (shellVm == null) return;

        // Close password modal and open keys display modal
        shellVm.HideModal();

        // Use Dispatcher to allow the close animation to start before opening next modal
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var keysModal = new PrivateKeysDisplayModal(
                _projectId, _founderKey, _recoveryKey,
                _nostrNpub, _nip05, _nostrNsec, _nostrHex);
            shellVm.ShowModal(keysModal);
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
}
