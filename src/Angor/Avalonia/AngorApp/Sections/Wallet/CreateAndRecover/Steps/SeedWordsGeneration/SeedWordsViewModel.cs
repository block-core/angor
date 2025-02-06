using System.Reactive.Linq;
using System.Threading.Tasks;
using Angor.UI.Model;
using AngorApp.Sections.Browse;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.ValueTasks;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.SeedWordsGeneration;

public partial class SeedWordsViewModel : ReactiveValidationObject, ISeedWordsViewModel
{
    public SeedWordsViewModel(UIServices uiServices)
    {
        GenerateWords = ReactiveCommand.CreateFromTask(() =>
        {
            return words.Match(
                async wordList => 
                {
                    var dialogResult = await ShowConfirmation(uiServices);
                    return dialogResult.Match(confirmed => confirmed ? CreateNewWords() : wordList, () => wordList).AsMaybe();
                },
                () => Task.FromResult(CreateNewWords().AsMaybe())
            );
        });

        GenerateWords.Values().Do(_ => AreWordsWrittenDown = false).Subscribe();

        wordsHelper = GenerateWords.ToProperty(this, x => x.Words);
    }

    private static SeedWords CreateNewWords()
    {
        return SampleData.Seedwords;
    }

    private static Task<Maybe<bool>> ShowConfirmation(UIServices uiServices)
    {
        return uiServices.Dialog.ShowConfirmation("Do you want to generate new seed words?",
            "You will see a different set of seed words. Make sure not to mix them with the ones you just saw and write down only the new ones.");
    }

    public ReactiveCommand<Unit,Maybe<SeedWords>> GenerateWords { get; }
    
    [Reactive] private bool areWordsWrittenDown;

    [ObservableAsProperty] private Maybe<SeedWords> words;
    public IObservable<bool> IsValid => this.WhenAnyValue<SeedWordsViewModel, bool, bool, Maybe<SeedWords>>(x => x.AreWordsWrittenDown, x => x.Words, (written, maybeWords) => written && maybeWords.HasValue);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;

    public Maybe<string> Title => "Generate your seed words";
}