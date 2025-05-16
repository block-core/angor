using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.EncryptionPassword;

public partial class EncryptionPasswordViewModel : ReactiveValidationObject, IEncryptionPasswordViewModel
{
    [Reactive] private string? encryptionKey;
    [Reactive] private string? passwordConfirm;

    public EncryptionPasswordViewModel()
    {
        this.ValidationRule(x => x.EncryptionKey, s => !string.IsNullOrWhiteSpace(s), "Password cannot be empty");
        this.ValidationRule(x => x.PasswordConfirm!, this.WhenAnyValue(x => x.EncryptionKey!, x => x.PasswordConfirm!, Equals),
            "Passwords do not match");
    }
}