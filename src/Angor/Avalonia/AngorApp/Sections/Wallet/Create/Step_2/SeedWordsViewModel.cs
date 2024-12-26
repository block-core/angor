using System.Linq;
using System.Reactive.Linq;
using AngorApp.Model;
using AngorApp.Sections.Browse;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Wallet.Create.Step_2;

public partial class SeedWordsViewModel : ReactiveValidationObject, ISeedWordsViewModel
{
    public SeedWordsViewModel()
    {
        GenerateWords = ReactiveCommand.Create(() =>
        {
            var wordlist = SampleData.Seedwords.AsMaybe();
            return wordlist;
        });

        GenerateWords.Do(_ => AreWordsWrittenDown = false).Subscribe();

        wordsHelper = GenerateWords.ToProperty(this, x => x.Words);
    }

    public ReactiveCommand<Unit,Maybe<WordList>> GenerateWords { get; }
    
    [Reactive] private bool areWordsWrittenDown;

    [ObservableAsProperty] private Maybe<WordList> words;
    public IObservable<bool> IsValid => this.WhenAnyValue(x => x.AreWordsWrittenDown, x => x.Words, (written, maybeWords) => written && maybeWords.HasValue);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;

    public Maybe<string> Title => "Generate your seed words";
}