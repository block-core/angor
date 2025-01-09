using AngorApp.Model;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.Sections.Wallet.Create.Step6;

public partial class SummaryAndCreationViewModel : ReactiveValidationObject, IStep, ISummaryAndCreationViewModel
{
    [ObservableAsProperty] private IWallet wallet;

    public SummaryAndCreationViewModel(Maybe<string> passphrase, WordList seedwords, string encryptionKey, IWalletBuilder walletBuilder)
    {
        Passphrase = passphrase;
        Seedwords = seedwords;
        EncryptionKey = encryptionKey;
        CreateWallet = ReactiveCommand.CreateFromTask(() => walletBuilder.Create(seedwords, passphrase, encryptionKey));
        walletHelper = CreateWallet.Successes().ToProperty(this, x => x.Wallet);
    }

    public WordList Seedwords { get; }
    public string EncryptionKey { get; }

    public IObservable<bool> IsValid => this.WhenAnyValue<SummaryAndCreationViewModel, IWallet>(x => x.Wallet).NotNull();
    public IObservable<bool> IsBusy => CreateWallet.IsExecuting;
    public bool AutoAdvance => true;

    public ReactiveCommand<Unit, Result<IWallet>> CreateWallet { get; }
    public Maybe<string> Passphrase { get; }
    public Maybe<string> Title => "Summary";
}