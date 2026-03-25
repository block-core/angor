using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using App.UI.Shared.Helpers;
using App.UI.Shell;
using Projektanker.Icons.Avalonia;

namespace App.UI.Shared.Controls;

/// <summary>
/// Private Keys Display — shell modal showing 7 key fields.
/// Vue ref: ManageFunds.vue lines 613-832.
/// Opened from PrivateKeysPasswordModal after successful password entry.
/// </summary>
public partial class PrivateKeysDisplayModal : UserControl, IBackdropCloseable
{
    private readonly string _projectId;
    private readonly string _founderKey;
    private readonly string _recoveryKey;
    private readonly string _nostrNpub;
    private readonly string _nip05;
    private readonly string _nostrNsec;
    private readonly string _nostrHex;

    private bool _nsecRevealed;
    private bool _hexRevealed;

    /// <summary>Parameterless ctor for XAML designer.</summary>
    public PrivateKeysDisplayModal() : this("", "", "", "", "", "", "") { }

    public PrivateKeysDisplayModal(
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

        // Populate all TextBox fields with stubbed key data
        SetFieldText("ProjectIdField", _projectId);
        SetFieldText("FounderKeyField", _founderKey);
        SetFieldText("RecoveryKeyField", _recoveryKey);
        SetFieldText("NostrNpubField", _nostrNpub);
        SetFieldText("Nip05Field", _nip05);
        SetFieldText("NsecTextBox", _nostrNsec);
        SetFieldText("HexTextBox", _nostrHex);

        // ── Close button ──
        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += OnCloseClick;

        // ── Eye toggle buttons ──
        var toggleNsecBtn = this.FindControl<Button>("ToggleNsecBtn");
        if (toggleNsecBtn != null) toggleNsecBtn.Click += OnToggleNsecClick;

        var toggleHexBtn = this.FindControl<Button>("ToggleHexBtn");
        if (toggleHexBtn != null) toggleHexBtn.Click += OnToggleHexClick;

        // ── Copy buttons ──
        WireCopy("CopyProjectIdBtn", _projectId);
        WireCopy("CopyFounderKeyBtn", _founderKey);
        WireCopy("CopyRecoveryKeyBtn", _recoveryKey);
        WireCopy("CopyNostrNpubBtn", _nostrNpub);
        WireCopy("CopyNip05Btn", _nip05);
        WireCopy("CopyNsecBtn", _nostrNsec);
        WireCopy("CopyHexBtn", _nostrHex);
    }

    private ShellViewModel? ShellVm =>
        this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;

    public void OnBackdropCloseRequested() { }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        ShellVm?.HideModal();
    }

    // ─────────────────────────────────────────────────────────────────
    //  EYE TOGGLE — nsec / hex fields
    //  Uses fa-solid only (fa-regular is broken in this project)
    // ─────────────────────────────────────────────────────────────────

    private void OnToggleNsecClick(object? sender, RoutedEventArgs e)
    {
        var nsecBox = this.FindControl<TextBox>("NsecTextBox");
        var nsecIcon = this.FindControl<Icon>("NsecEyeIcon");
        if (nsecBox == null) return;

        _nsecRevealed = !_nsecRevealed;
        if (_nsecRevealed)
        {
            nsecBox.PasswordChar = default;
            if (nsecIcon != null) nsecIcon.Value = "fa-solid fa-eye-slash";
        }
        else
        {
            nsecBox.PasswordChar = '*';
            if (nsecIcon != null) nsecIcon.Value = "fa-solid fa-eye";
        }
    }

    private void OnToggleHexClick(object? sender, RoutedEventArgs e)
    {
        var hexBox = this.FindControl<TextBox>("HexTextBox");
        var hexIcon = this.FindControl<Icon>("HexEyeIcon");
        if (hexBox == null) return;

        _hexRevealed = !_hexRevealed;
        if (_hexRevealed)
        {
            hexBox.PasswordChar = default;
            if (hexIcon != null) hexIcon.Value = "fa-solid fa-eye-slash";
        }
        else
        {
            hexBox.PasswordChar = '*';
            if (hexIcon != null) hexIcon.Value = "fa-solid fa-eye";
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────

    private void SetFieldText(string textBoxName, string text)
    {
        var tb = this.FindControl<TextBox>(textBoxName);
        if (tb != null) tb.Text = text;
    }

    private void WireCopy(string buttonName, string text)
    {
        var btn = this.FindControl<Button>(buttonName);
        if (btn != null)
            btn.Click += (_, _) => ClipboardHelper.CopyToClipboard(this, text);
    }
}
