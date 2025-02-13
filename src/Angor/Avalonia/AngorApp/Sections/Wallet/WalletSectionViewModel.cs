using System.Reactive.Linq;
using System.Windows.Input;
using Angor.UI.Model;
using AngorApp.Sections.Wallet.Operate;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet;

public partial class WalletSectionViewModel : ReactiveObject, IWalletSectionViewModel
{
    [ObservableAsProperty] private IWalletViewModel? wallet;
    
    public WalletSectionViewModel(IWalletFactory walletFactory, IWalletProvider walletProvider, UIServices services)
    {
        CreateWallet = ReactiveCommand.CreateFromTask(walletFactory.Create);
        CreateWallet.Values().Successes().Do(walletProvider.SetWallet).Subscribe();
        RecoverWallet = ReactiveCommand.CreateFromTask(walletFactory.Recover);
        
        walletHelper = services.ActiveWallet.CurrentChanged
            .Merge(Observable.Return(services.ActiveWallet.Current).Values())
            .Select(w => new WalletViewModel(w, services))
            .ToProperty(this, x => x.Wallet);
    }

    public ReactiveCommand<Unit, Maybe<Result<IWallet>>> CreateWallet { get; }
    public ReactiveCommand<Unit, Maybe<Result<IWallet>>> RecoverWallet { get; }
}