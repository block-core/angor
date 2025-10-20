using System.Threading.Tasks;
using Angor.Contexts.Wallet.Application;
using AngorApp.Core;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Wallet.CreateAndImport.Steps.SeedWordsGeneration;

public partial class SeedWordsViewModel : ReactiveValidationObject, ISeedWordsViewModel, IValidatable
{
    private readonly IWalletAppService walletAppService;

    public SeedWordsViewModel(IWalletAppService walletAppService, UIServices uiServices)
    {
        this.walletAppService = walletAppService;
        // Start without seed words
        words = new SafeMaybe<SeedWords>(Maybe<SeedWords>.None);

        GenerateWords = ReactiveCommand.CreateFromTask(async () =>
        {
            if (words.HasValue)
            {
                // Existing words? ask for others
                var dialogResult = await ShowConfirmation(uiServices);
                var newWords = dialogResult.Match(
                    confirmed => confirmed ? CreateNewWords() : words.Value,
                    () => words.Value);
                return newWords.AsSafeMaybe();
            }
            else
            {
                // No seedwords? Create them
                return CreateNewWords().AsSafeMaybe();
            }
        });

        GenerateWords
            .Do(_ => AreWordsWrittenDown = false)
            .Subscribe();

        wordsHelper = GenerateWords.ToProperty(this, x => x.Words);
    }

    private SeedWords CreateNewWords()
    {
        return new SeedWords(walletAppService.GenerateRandomSeedwords());
    }

    private static Task<Maybe<bool>> ShowConfirmation(UIServices uiServices)
    {
        return uiServices.Dialog.ShowConfirmation(
            "Do you want to generate new seed words?",
            "You will see a different set of seed words. Make sure not to mix them with the ones you just saw and write down only the new ones.");
    }

    public ReactiveCommand<Unit, SafeMaybe<SeedWords>> GenerateWords { get; }

    [Reactive] private bool areWordsWrittenDown;

    [ObservableAsProperty] private SafeMaybe<SeedWords> words;

    public IObservable<bool> IsValid => this.WhenAnyValue<SeedWordsViewModel, bool, bool, SafeMaybe<SeedWords>>(x => x.AreWordsWrittenDown, x => x.Words,
        (written, safeWords) => written && safeWords.HasValue);

    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;

    public Maybe<string> Title => "Generate your seed words";
}
