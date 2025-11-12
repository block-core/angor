using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.Passphrase.Create;

public partial class PassphraseCreateViewModel() : ReactiveValidationObject, IPassphraseCreateViewModel, IValidatable
{
    [Reactive] private string? passphrase;

    public IObservable<bool> IsValid => this.IsValid();
    public Maybe<string> Title => "Optional BIP39 Passphrase";
}
