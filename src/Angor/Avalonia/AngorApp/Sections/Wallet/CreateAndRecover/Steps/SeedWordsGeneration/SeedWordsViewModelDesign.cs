using System.Reactive.Linq;
using Angor.UI.Model;
using AngorApp.Core;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using SampleData = AngorApp.Sections.Browse.SampleData;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.SeedWordsGeneration;

public partial class SeedWordsViewModelDesign : ReactiveObject, ISeedWordsViewModel
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