namespace App.UI.Shared.Controls;

/// <summary>
/// ViewModel for the Private Keys Password modal — owns password validation state.
/// Follows the [Reactive] validation pattern established by CreateProjectViewModel.
/// Vue ref: ManageFunds.vue lines 559-611.
/// </summary>
public partial class PrivateKeysPasswordModalViewModel : ReactiveObject
{
    // ── Form input ──
    [Reactive] private string password = "";

    // ── Validation ──
    [Reactive] private string passwordError = "";

    public bool HasPasswordError => !string.IsNullOrEmpty(PasswordError);

    // ── Key data (passed through to PrivateKeysDisplayModal) ──
    public string ProjectId { get; }
    public string FounderKey { get; }
    public string RecoveryKey { get; }
    public string NostrNpub { get; }
    public string Nip05 { get; }
    public string NostrNsec { get; }
    public string NostrHex { get; }

    public PrivateKeysPasswordModalViewModel(
        string projectId, string founderKey, string recoveryKey,
        string nostrNpub, string nip05, string nostrNsec, string nostrHex)
    {
        ProjectId = projectId;
        FounderKey = founderKey;
        RecoveryKey = recoveryKey;
        NostrNpub = nostrNpub;
        Nip05 = nip05;
        NostrNsec = nostrNsec;
        NostrHex = nostrHex;

        // Clear error on typing (Vue: @input clears errors)
        this.WhenAnyValue(x => x.Password)
            .Subscribe(_ =>
            {
                PasswordError = "";
                this.RaisePropertyChanged(nameof(HasPasswordError));
            });
    }

    /// <summary>
    /// Validate password. Returns true if valid.
    /// </summary>
    public bool ValidateAndViewKeys()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = "Password is required";
            this.RaisePropertyChanged(nameof(HasPasswordError));
            return false;
        }

        return true;
    }
}
