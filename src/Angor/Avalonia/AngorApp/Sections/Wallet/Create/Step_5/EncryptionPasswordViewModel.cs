using System.Reactive.Linq;
using AngorApp.Model;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.Create.Step_5;

public partial class EncryptionPasswordViewModel : ReactiveValidationObject, IStep, IEncryptionPasswordViewModel
{
    [Reactive] private string? password;
    [Reactive] private string? passwordConfirm;

    public EncryptionPasswordViewModel(WordList seedWords, Maybe<string> passphrase)
    {
        SeedWords = seedWords;
        Passphrase = passphrase;
        this.ValidationRule(x => x.Password, s => !string.IsNullOrWhiteSpace(s), "Password cannot be empty");
        this.ValidationRule(x => x.PasswordConfirm, this.WhenAnyValue(x => x.Password, x => x.PasswordConfirm, Equals), "Passwords do not match");
    }

    public WordList SeedWords { get; }
    public Maybe<string> Passphrase { get; }

    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    public Maybe<string> Title => "Encryption password";
}