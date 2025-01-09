using System.Reactive.Linq;
using AngorApp.Model;
using AngorApp.Sections.Browse;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;

namespace AngorApp.Sections.Wallet.Create.Step2;

public partial class SeedWordsViewModelDesign : ReactiveObject, ISeedWordsViewModel
{
    private bool hasWords;
    public IObservable<bool> IsValid { get; } = Observable.Return(true);
    public IObservable<bool> IsBusy { get; } = Observable.Return(false);
    public bool AutoAdvance => false; 
    
    [Reactive] private Maybe<WordList> words = Maybe<WordList>.None;

    public bool HasWords
    {
        get => hasWords;
        set
        {
            if (value == hasWords)
                return;

            if (value)
            {
                Words = SampleData.Seedwords.AsMaybe();                
            }
            else
            {
                Words = Maybe<WordList>.None;
            }

            hasWords = value;
            
            this.RaiseAndSetIfChanged(ref hasWords, value);
        }
    }

    public ReactiveCommand<Unit, Maybe<WordList>> GenerateWords { get; }
    [Reactive] private bool areWordsWrittenDown;
}