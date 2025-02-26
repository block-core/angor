using System.Collections.ObjectModel;
using System.Windows.Input;
using Angor.Wallet.Domain;
using AngorApp.Core;
using Zafiro.UI;

namespace AngorApp.Sections.Wallet.Operate;

public interface IWalletViewModel
{
    IWallet Wallet { get; }
    ICommand Send { get; }
    string Name { get; set; }
    ReactiveCommand<Unit, ResultViewModel<string>> GetReceiveAddress { get; }
    ResultViewModel<string> ReceiveAddressResult { get; }
    StoppableCommand<Unit, Result<BroadcastedTransaction>> SyncCommand { get; }
    bool HasSomething { get; }
    IObservable<bool> IsLoading { get; }
    ReadOnlyObservableCollection<ITransactionViewModel> History { get; }
}