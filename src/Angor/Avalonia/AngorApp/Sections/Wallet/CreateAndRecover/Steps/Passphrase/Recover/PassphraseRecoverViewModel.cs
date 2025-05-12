using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.Passphrase.Recover;

public partial class PassphraseRecoverViewModel : ReactiveValidationObject, IPassphraseRecoverViewModel
{
    [Reactive] private string? passphrase;
    public IObservable<bool> IsValid => Observable.Return(true);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    public Maybe<string> Title => "Optional BIP39 Passphrase";
}