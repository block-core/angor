using System.Reactive.Linq;
using Angor.UI.Model;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.Passphrase.Recover;

public partial class PassphraseRecoverViewModel : ReactiveValidationObject, IStep, IPassphraseRecoverViewModel
{
    [Reactive] private string? passphrase;

    public PassphraseRecoverViewModel(SeedWords seedWords)
    {
        SeedWords = seedWords;
    }

    public SeedWords SeedWords { get; }
    public IObservable<bool> IsValid => Observable.Return(true);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    public Maybe<string> Title => "Optional BIP39 Passphrase";
}