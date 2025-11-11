using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.RecoverySeedWords;

public partial class RecoverySeedWordsViewModel : ReactiveValidationObject, IRecoverySeedWordsViewModel, IValidatable
{
    [Reactive] private string? rawWordList;

    public RecoverySeedWordsViewModel()
    {
        this.ValidationRule<RecoverySeedWordsViewModel, string>(x => x.RawWordList, x => !string.IsNullOrWhiteSpace(x), "Please, enter your 12 seed words separated by spaces");
        this.ValidationRule<RecoverySeedWordsViewModel, string>(x => x.RawWordList, x => x is null || x.Split(" ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length == 12, "We need exactly 12 words");
    }

    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    public Maybe<string> Title => "Enter your seed words";
    public SeedWords SeedWords => new SeedWords(RawWordList ?? "");
}
