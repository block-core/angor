using System.Reactive.Linq;
using System.Windows.Input;
using AngorApp.Model;
using AngorApp.Sections.Wallet.NoWallet;
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
        CreateWallet.Successes().Do(walletProvider.SetWallet).Subscribe();
        walletHelper = CreateWallet.Successes().Select(wallet => new WalletViewModel(wallet, services)).ToProperty<WalletSectionViewModel, IWalletViewModel>(this, x => x.Wallet);
        Recover = ReactiveCommand.Create(() => { });
    }

    public ReactiveCommand<Unit, Result<IWallet>> CreateWallet { get; }
    public ICommand Recover { get; }
}