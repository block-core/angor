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
    [ObservableAsProperty] private IWalletViewModel? activeWallet;
    [ObservableAsProperty] private bool canCreateWallet;
    
    public WalletSectionViewModel(UIServices uiServices, WalletCreationWizard creationWizard,
        WalletRecoveryWizard recoveryWizard,
        IWalletAppService walletAppService)
    {
        CreateWallet = ReactiveCommand.CreateFromTask(creationWizard.Start);
        RecoverWallet = ReactiveCommand.CreateFromTask(recoveryWizard.Start);
        
        LoadWallet = ReactiveCommand.CreateFromTask(() => uiServices.WalletRoot.GetDefaultWalletAndActivate());
        LoadWallet.HandleErrorsWith(uiServices.NotificationService, "Failed to load wallet");
        
        activeWalletHelper = uiServices.ActiveWallet.CurrentChanged
            .Select(w => new WalletViewModel(w, walletAppService, uiServices))
            .ToProperty(this, x => x.ActiveWallet);
            
        HasWallet = this.WhenAnyValue(x => x.ActiveWallet).NotNull();
        canCreateWalletHelper = uiServices.WalletRoot.HasDefault().Not().ToProperty(this, x => x.CanCreateWallet);
        IsBusy = LoadWallet.IsExecuting;
        LoadWallet.Execute().Subscribe();
    }
    
    public IObservable<bool> HasWallet { get; }
    public IObservable<bool> IsBusy { get; }

    public ReactiveCommand<Unit, Result<Maybe<IWallet>>> LoadWallet { get; }
    public ReactiveCommand<Unit, Maybe<Unit>> CreateWallet { get; }
    public ReactiveCommand<Unit, Maybe<Unit>> RecoverWallet { get; }
}