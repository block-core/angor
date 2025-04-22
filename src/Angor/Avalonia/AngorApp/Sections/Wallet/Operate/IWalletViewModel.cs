using System.Windows.Input;
using Angor.Contexts.Wallet.Domain;
using Zafiro.UI;

namespace AngorApp.Sections.Wallet.Operate;

public interface IWalletViewModel
{
    public IWallet Wallet { get; }
    public ICommand Send { get; }
    public ReactiveCommand<Unit, ResultViewModel<string>> GetReceiveAddress { get; }
    public ResultViewModel<string> ReceiveAddressResult { get; }
    StoppableCommand<Unit, Result<BroadcastedTransaction>> Sync { get; set; }
    public IEnumerable<IdentityContainer<TransactionViewModel>> History { get; }
}