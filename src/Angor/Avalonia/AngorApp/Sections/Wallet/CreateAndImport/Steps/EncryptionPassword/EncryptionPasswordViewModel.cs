using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Wallet.CreateAndImport.Steps.EncryptionPassword;

public partial class EncryptionPasswordViewModel : ReactiveValidationObject, IEncryptionPasswordViewModel
{
    [Reactive] private string? encryptionKey;
    [Reactive] private string? passwordConfirm;

    public EncryptionPasswordViewModel()
    {
        this.ValidationRule<EncryptionPasswordViewModel, string>(x => x.EncryptionKey, s => !string.IsNullOrWhiteSpace(s), "Password cannot be empty");
        this.ValidationRule<EncryptionPasswordViewModel, string>(x => x.PasswordConfirm!, this.WhenAnyValue<EncryptionPasswordViewModel, bool, string, string>(x => x.EncryptionKey!, x => x.PasswordConfirm!, Object.Equals),
            "Passwords do not match");
    }
}