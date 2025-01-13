using AngorApp.Model;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.SummaryAndCreation;

public partial class SummaryAndCreationViewModel : ReactiveValidationObject, IStep, ISummaryAndCreationViewModel
{
    [ObservableAsProperty] private IWallet? wallet;

    public SummaryAndCreationViewModel(Maybe<string> passphrase, SeedWords seedwords, string encryptionKey, IWalletBuilder walletBuilder)
    {
        Passphrase = passphrase;
        CreateWallet = ReactiveCommand.CreateFromTask(() => walletBuilder.Create(seedwords, passphrase, encryptionKey));
        walletHelper = CreateWallet.Successes().ToProperty(this, x => x.Wallet);
    }

    public string CreateWalletText => IsRecovery ? "Recover Wallet" : "Create Wallet";
    public string CreatingWalletText => IsRecovery ? "Recovering Wallet..." : "Creating Wallet...";
    public string TitleText => IsRecovery ? "You are all set to recover your wallet" : "You are all set to create your wallet";
    public required bool IsRecovery { get; init; }
    
    public IObservable<bool> IsValid => this.WhenAnyValue<SummaryAndCreationViewModel, IWallet>(x => x.Wallet).NotNull();
    public IObservable<bool> IsBusy => CreateWallet.IsExecuting;
    public bool AutoAdvance => true;
    public ReactiveCommand<Unit, Result<IWallet>> CreateWallet { get; }
    public Maybe<string> Passphrase { get; }
    public Maybe<string> Title => "Summary";
}