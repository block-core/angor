using System.Reactive.Linq;
using Angor.UI.Model;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.EncryptionPassword;

public partial class EncryptionPasswordViewModel : ReactiveValidationObject, IStep, IEncryptionPasswordViewModel
{
    [Reactive] private string? password;
    [Reactive] private string? passwordConfirm;

    public EncryptionPasswordViewModel(SeedWords seedWords, Maybe<string> passphrase)
    {
        SeedWords = seedWords;
        Passphrase = passphrase;
        this.ValidationRule<EncryptionPasswordViewModel, string>(x => x.Password, s => !string.IsNullOrWhiteSpace(s), "Password cannot be empty");
        this.ValidationRule<EncryptionPasswordViewModel, string>(x => x.PasswordConfirm, this.WhenAnyValue<EncryptionPasswordViewModel, bool, string, string>(x => x.Password, x => x.PasswordConfirm, Object.Equals), "Passwords do not match");
    }

    public SeedWords SeedWords { get; }
    public Maybe<string> Passphrase { get; }

    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    public Maybe<string> Title => "Encryption password";
}