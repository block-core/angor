using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Wallet.CreateAndImport.Steps.Passphrase.Create;

public partial class PassphraseCreateViewModel() : ReactiveValidationObject, IPassphraseCreateViewModel
{
    [Reactive] private string? passphrase;

    public Maybe<string> Title => "Optional BIP39 Passphrase";
}