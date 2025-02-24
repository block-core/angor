using System.Windows.Input;
using AngorApp.Sections.Wallet.CreateAndRecover;
using AngorApp.Sections.Wallet.Operate;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet;

public partial class WalletSectionViewModel : ReactiveObject, IWalletSectionViewModel
{
    [ObservableAsProperty] private IWalletViewModel? wallet;

    public WalletSectionViewModel(UIServices services, IWalletWizard walletWizard)
    {
        CreateWallet = ReactiveCommand.CreateFromTask(() => walletWizard.CreateNew());
        RecoverWallet = ReactiveCommand.CreateFromTask(() => walletWizard.Recover());

        walletHelper = services.ActiveWallet.CurrentChanged
            .Merge(Observable.Return(services.ActiveWallet.Current).Values())
            .Select(w => new WalletViewModel(w, services))
            .ToProperty(this, x => x.Wallet);
    }

    public ReactiveCommand<Unit, Maybe<Unit>> CreateWallet { get; }
    public ReactiveCommand<Unit, Maybe<Unit>> RecoverWallet { get; }
}