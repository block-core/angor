using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Blockcore.NBitcoin.BIP39;

namespace AngorApp.UI.Flows.AddWallet.Import
{
    public partial class SeedwordsEntryViewModel : ReactiveValidationObject, IValidatable
    {
        public SeedwordsEntryViewModel()
        {
            this.ValidationRule(x => x.Seedwords, x => x is null || HasValidSeedwordLength(x), "Please, enter 12 or 24 seed words separated by spaces");
            this.ValidationRule(x => x.Seedwords, x => x is null || HasOnlyValidEnglishMnemonicWords(x), "Please, enter valid BIP-39 English seed words");
            this.ValidationRule(this.WhenAnyValue(x => x.Seedwords), x => x is not null, _ =>  "Seed words cannot be empty");
        }
        
        [Reactive]
        private string? seedwords;

        public IObservable<bool> IsValid => this.IsValid();

        private static bool HasValidSeedwordLength(string seedwords)
        {
            var words = seedwords.Split(" ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length;
            return words is 12 or 24;
        }

        private static bool HasOnlyValidEnglishMnemonicWords(string seedwords)
        {
            var words = seedwords.Split(" ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return words.All(word => Wordlist.English.WordExists(word.ToLowerInvariant(), out _));
        }
    }
}
