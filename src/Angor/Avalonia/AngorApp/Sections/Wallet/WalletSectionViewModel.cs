using Angor.Contexts.Wallet.Application;
using AngorApp.Sections.Wallet.CreateAndRecover;
using AngorApp.Sections.Wallet.Operate;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;
using WalletViewModel = AngorApp.Sections.Wallet.Operate.WalletViewModel;

namespace AngorApp.Sections.Wallet;

public partial class WalletSectionViewModel : ReactiveObject, IWalletSectionViewModel
{
    [ObservableAsProperty] private IWalletViewModel? wallet;

    public WalletSectionViewModel(UIServices uiServices, IWalletWizard walletWizard, IWalletAppService walletAppService, IWalletBuilder walletBuilder)
    {
        CreateWallet = ReactiveCommand.CreateFromTask(() => walletWizard.CreateNew());
        RecoverWallet = ReactiveCommand.CreateFromTask(() => walletWizard.Recover());

        walletHelper = uiServices.ActiveWallet.CurrentChanged
            .Merge(Observable.Return(uiServices.ActiveWallet.Current).Values())
            .Select(w => new WalletViewModel(w, walletAppService, uiServices))
            .ToProperty(this, x => x.Wallet);

        TryLoadExistingWallet = ReactiveCommand.CreateFromTask(() => uiServices.ActiveWallet.TryGetCurrent());
        walletHelper = TryLoadExistingWallet.Successes().Values()
            .Merge(uiServices.ActiveWallet.CurrentChanged)
            .Select(w => new WalletViewModel(w, walletAppService, uiServices)).ToProperty(this, x => x.Wallet);

        HasWallet = this.WhenAnyValue(x => x.Wallet).NotNull();

        IsBusy = TryLoadExistingWallet.IsExecuting;
        TryLoadExistingWallet.Execute().Subscribe();
        TryLoadExistingWallet.HandleErrorsWith(uiServices.NotificationService, "Failed to load wallet");
        ShowCreateAndRecover = TryLoadExistingWallet.Successes().Empties().Any().StartWith(false);
    }

    public IObservable<bool> HasWallet { get; set; }
    public IObservable<bool> ShowCreateAndRecover { get; }

    public ReactiveCommand<Unit,Result<Maybe<IWallet>>> TryLoadExistingWallet { get; set; }
    public IObservable<bool> IsBusy { get; }
    public ReactiveCommand<Unit, Maybe<Unit>> CreateWallet { get; }
    public ReactiveCommand<Unit, Maybe<Unit>> RecoverWallet { get; }
}