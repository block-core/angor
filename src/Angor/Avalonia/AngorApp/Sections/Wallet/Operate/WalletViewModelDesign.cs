using System.Windows.Input;
using Angor.Contexts.Wallet.Domain;
using ReactiveUI.SourceGenerators;
using Zafiro.UI;

namespace AngorApp.Sections.Wallet.Operate;

public class WalletViewModelDesign : ReactiveObject, IWalletViewModel
{
    private ReactiveCommand<Unit, ResultViewModel<string>> getReceiveAddress;

    public IWallet Wallet { get; } = new WalletDesign();

    public ICommand Send { get; }
    public string Name { get; init; } = "Test Wallet";

    ReactiveCommand<Unit, ResultViewModel<string>> IWalletViewModel.GetReceiveAddress => getReceiveAddress;

    public ResultViewModel<string> ReceiveAddressResult { get; }
    public StoppableCommand<Unit, Result<BroadcastedTransaction>> Sync { get; set; }
    public IEnumerable<IdentityContainer<TransactionViewModel>> History { get; }
}