using System.Threading.Tasks;
using Angor.UI.Model;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.SummaryAndCreation;

public partial class SummaryAndCreationViewModel : ReactiveValidationObject, IStep, ISummaryAndCreationViewModel
{
    private readonly IWalletBuilder walletBuilder;
    private readonly UIServices uiServices;
    [ObservableAsProperty] private IWallet? wallet;

    public SummaryAndCreationViewModel(Maybe<string> passphrase, SeedWords seedwords, string encryptionKey, IWalletBuilder walletBuilder, UIServices uiServices)
    {
        this.walletBuilder = walletBuilder;
        this.uiServices = uiServices;
        Passphrase = passphrase;
        CreateWallet = ReactiveCommand.CreateFromTask(() => CreateAndSet(seedwords, encryptionKey, encryptionKey));
        walletHelper = CreateWallet.Successes().ToProperty(this, x => x.Wallet);
    }
    
    private Task<Result<IWallet>> CreateAndSet(SeedWords seedwords, Maybe<string> passphrase, string encryptionKey)
    {
        return walletBuilder.Create(seedwords, passphrase, encryptionKey)
            .Tap(w => uiServices.ActiveWallet.Current = w.AsMaybe());
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