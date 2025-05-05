using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.EncryptionPassword;

public partial class EncryptionPasswordViewModel : ReactiveValidationObject, IStep, IEncryptionPasswordViewModel
{
    [Reactive] private string? encryptionKey;
    [Reactive] private string? passwordConfirm;

    public EncryptionPasswordViewModel()
    {
        this.ValidationRule<EncryptionPasswordViewModel, string>(x => x.EncryptionKey, s => !string.IsNullOrWhiteSpace(s), "Password cannot be empty");
        this.ValidationRule<EncryptionPasswordViewModel, string>(x => x.PasswordConfirm!, this.WhenAnyValue<EncryptionPasswordViewModel, bool, string, string>(x => x.EncryptionKey!, x => x.PasswordConfirm!, Equals),
            "Passwords do not match");
    }

    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    public Maybe<string> Title => "Encryption password";
}