using System.Windows.Input;
using Angor.Contexts.Wallet.Domain;
using SuppaWallet.Gui.Wallet.Main;
using Zafiro.UI;

namespace AngorApp.Sections.Wallet.Operate;

public interface IWalletViewModel
{
    public IWallet Wallet { get; }
    public ICommand Send { get; }
    public string Name { get; init; }
    public ReactiveCommand<Unit, ResultViewModel<string>> GetReceiveAddress { get; }
    public ResultViewModel<string> ReceiveAddressResult { get; }
    StoppableCommand<Unit, Result<BroadcastedTransaction>> Sync { get; set; }
    public WalletDisplayStatus WalletDisplayStatus { get; }
    public IEnumerable<IdentityContainer<TransactionViewModel>> History { get; }
}