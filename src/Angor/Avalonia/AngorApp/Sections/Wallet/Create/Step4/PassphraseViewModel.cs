using System.Reactive.Linq;
using AngorApp.Model;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.Create.Step4;

public partial class PassphraseViewModel : ReactiveValidationObject, IStep, IPassphraseViewModel
{
    [Reactive] private string? passphrase;

    public PassphraseViewModel(WordList seedWords)
    {
        SeedWords = seedWords;
    }

    public WordList SeedWords { get; }
    public IObservable<bool> IsValid => Observable.Return(true);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    public Maybe<string> Title => "Optional BIP39 Passphrase";
}