using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.Summary;

public partial class SummaryViewModel : ReactiveValidationObject, ISummaryViewModel, IValidatable
{
    private readonly IWalletAppService walletAppService;
    private readonly IWalletProvider walletProvider;
    private readonly UIServices uiServices;
    private readonly IWalletContext walletContext;
    private readonly WalletImportOptions options;
    private readonly Func<BitcoinNetwork> getNetwork;
    [ObservableAsProperty] private IWallet? wallet;

    public SummaryViewModel(IWalletAppService walletAppService, IWalletProvider walletProvider, UIServices uiServices, IWalletContext walletContext, WalletImportOptions options, Func<BitcoinNetwork> getNetwork)
    {
        this.walletAppService = walletAppService;
        this.walletProvider = walletProvider;
        this.uiServices = uiServices;
        this.walletContext = walletContext;
        this.options = options;
        this.getNetwork = getNetwork;
        Passphrase = options.Passphrase;
        CreateWallet = ReactiveCommand.CreateFromTask(() => CreateAndActivate());
        walletHelper = CreateWallet.Successes().ToProperty(this, x => x.Wallet);
    }

    private Task<Result<IWallet>> CreateAndActivate()
    {
        return walletAppService.CreateWallet("<default>", options.Seedwords.ToString(), options.Passphrase, options.EncryptionKey, getNetwork())
            .Bind(id => walletProvider.Get(id))
            .Tap(w => walletContext.CurrentWallet = w.AsMaybe());
    }

    public string CreateWalletText => IsRecovery ? "Import Wallet" : "Create Wallet";
    public string CreatingWalletText => IsRecovery ? "Importing Wallet..." : "Creating Wallet...";
    public string TitleText => IsRecovery ? "You are all set to import your wallet" : "You are all set to create your wallet";
    public required bool IsRecovery { get; init; }

    public IObservable<bool> IsValid => this.WhenAnyValue<SummaryViewModel, IWallet>(x => x.Wallet).NotNull();
    public IObservable<bool> IsBusy => CreateWallet.IsExecuting;
    public bool AutoAdvance => true;
    public ReactiveCommand<Unit, Result<IWallet>> CreateWallet { get; }
    public Maybe<string> Passphrase { get; }
    public Maybe<string> Title => "Summary";
}
