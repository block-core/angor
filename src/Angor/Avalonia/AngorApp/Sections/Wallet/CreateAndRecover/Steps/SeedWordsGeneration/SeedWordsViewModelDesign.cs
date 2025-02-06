using System.Reactive.Linq;
using Angor.UI.Model;
using AngorApp.Sections.Browse;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.SeedWordsGeneration;

public partial class SeedWordsViewModelDesign : ReactiveObject, ISeedWordsViewModel
{
    private bool hasWords;
    public IObservable<bool> IsValid { get; } = Observable.Return(true);
    public IObservable<bool> IsBusy { get; } = Observable.Return(false);
    public bool AutoAdvance => false; 
    
    [Reactive] private Maybe<SeedWords> words = Maybe<SeedWords>.None;

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
                Words = Maybe<SeedWords>.None;
            }

            hasWords = value;
            
            this.RaiseAndSetIfChanged(ref hasWords, value);
        }
    }

    public ReactiveCommand<Unit, Maybe<SeedWords>> GenerateWords { get; }
    [Reactive] private bool areWordsWrittenDown;
}