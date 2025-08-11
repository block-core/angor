using AngorApp.Sections.Browse;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Wallet.CreateAndImport.Steps.SeedWordsConfirmation;

public interface ISeedWordsConfirmationViewModel
{
    SeedWords SeedWords { get; }
    IEnumerable<Challenge> Challenges { get; }
}

public class SeedWordsConfirmationViewModelDesign : ISeedWordsConfirmationViewModel
{
    public SeedWords SeedWords { get; } = SampleData.Seedwords;
    public IEnumerable<Challenge> Challenges => [new(new SeedWord(3, "test")), new(new SeedWord(7, "hi"))];
}

public partial class Challenge : ReactiveValidationObject
{
    [Reactive] private string text;

    public Challenge(SeedWord word)
    {
        this.ValidationRule<Challenge, string>(x => x.Text, x => Equals(word.Text, x), "Word does not match");
        Word = word;
    }

    public SeedWord Word { get; }
}