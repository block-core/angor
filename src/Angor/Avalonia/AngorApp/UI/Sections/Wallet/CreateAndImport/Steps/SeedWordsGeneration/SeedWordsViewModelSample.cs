using AngorApp.Core;
using ReactiveUI.SourceGenerators;
using SampleData = AngorApp.UI.Sections.Browse.SampleData;

namespace AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.SeedWordsGeneration;

public partial class SeedWordsViewModelSample : ReactiveObject, ISeedWordsViewModel
{
    private bool hasWords;
    public IObservable<bool> IsValid { get; } = Observable.Return(true);
    public IObservable<bool> IsBusy { get; } = Observable.Return(false);
    public bool AutoAdvance => false; 
    
    [Reactive] private SafeMaybe<SeedWords> words = new(Maybe<SeedWords>.None);

    public bool HasWords
    {
        get => hasWords;
        set
        {
            if (value == hasWords)
                return;

            if (value)
            {
                Words = SampleData.Seedwords.AsSafeMaybe();                
            }
            else
            {
                Words = Maybe<SeedWords>.None.AsSafeMaybe();
            }

            hasWords = value;
            
            this.RaiseAndSetIfChanged(ref hasWords, value);
        }
    }

    public ReactiveCommand<Unit, SafeMaybe<SeedWords>> GenerateWords { get; }
    [Reactive] private bool areWordsWrittenDown;
}