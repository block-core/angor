using System.Reactive.Linq;
using Angor.UI.Model;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.Passphrase.Create;

public partial class PassphraseCreateViewModel(SeedWords seedWords) : ReactiveValidationObject, IStep, IPassphraseCreateViewModel
{
    [Reactive] private string? passphrase;

    public SeedWords SeedWords { get; } = seedWords;
    public IObservable<bool> IsValid => Observable.Return(true);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    public Maybe<string> Title => "Optional BIP39 Passphrase";
}