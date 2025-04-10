using System.Threading.Tasks;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.Summary;

public partial class SummaryViewModel : ReactiveValidationObject, IStep, ISummaryViewModel
{
    private readonly IWalletAppService walletAppService;
    private readonly IWalletBuilder walletBuilder;
    private readonly UIServices uiServices;
    private readonly WalletImportOptions options;
    private readonly Func<BitcoinNetwork> getNetwork;
    [ObservableAsProperty] private IWallet? wallet;

    public SummaryViewModel(IWalletAppService walletAppService, IWalletBuilder walletBuilder, UIServices uiServices, WalletImportOptions options, Func<BitcoinNetwork> getNetwork)
    {
        this.walletAppService = walletAppService;
        this.walletBuilder = walletBuilder;
        this.uiServices = uiServices;
        this.options = options;
        this.getNetwork = getNetwork;
        Passphrase = options.Passphrase;
        CreateWallet = ReactiveCommand.CreateFromTask(() => CreateAndActivate());
        walletHelper = CreateWallet.Successes().ToProperty(this, x => x.Wallet);
    }

    private Task<Result<IWallet>> CreateAndActivate()
    {
        return walletAppService.CreateWallet("<default>", options.Seedwords.ToString(), options.Passphrase, options.EncryptionKey, getNetwork())
            .Bind(id => walletBuilder.Get(id))
            .Tap(w => uiServices.ActiveWallet.SetCurrent(w));
    }

    public string CreateWalletText => IsRecovery ? "Recover Wallet" : "Create Wallet";
    public string CreatingWalletText => IsRecovery ? "Recovering Wallet..." : "Creating Wallet...";
    public string TitleText => IsRecovery ? "You are all set to recover your wallet" : "You are all set to create your wallet";
    public required bool IsRecovery { get; init; }

    public IObservable<bool> IsValid => this.WhenAnyValue<SummaryViewModel, IWallet>(x => x.Wallet).NotNull();
    public IObservable<bool> IsBusy => CreateWallet.IsExecuting;
    public bool AutoAdvance => true;
    public ReactiveCommand<Unit, Result<IWallet>> CreateWallet { get; }
    public Maybe<string> Passphrase { get; }
    public Maybe<string> Title => "Summary";
}