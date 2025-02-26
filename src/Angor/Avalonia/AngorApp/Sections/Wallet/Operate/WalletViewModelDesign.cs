using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Angor.Wallet.Domain;
using Zafiro.UI;

namespace AngorApp.Sections.Wallet.Operate;

public class WalletViewModelDesign : IWalletViewModel
{
    private ReactiveCommand<Unit, ResultViewModel<string>> getReceiveAddress;

    public IWallet Wallet { get; } = new WalletDesign
    {
    };

    public ICommand Send { get; }
    public string Name { get; set; }

    ReactiveCommand<Unit, ResultViewModel<string>> IWalletViewModel.GetReceiveAddress => getReceiveAddress;

    public ResultViewModel<string> ReceiveAddressResult { get; }
    public StoppableCommand<Unit, Result<BroadcastedTransaction>> SyncCommand { get; set; }
    public bool HasSomething { get; set; }
    public IObservable<bool> IsLoading { get; }
    public ReadOnlyObservableCollection<ITransactionViewModel> History { get; } = new(new ObservableCollection<ITransactionViewModel>(GetHistory()));

    private static IEnumerable<ITransactionViewModel> GetHistory()
    {
        return [
            new TransactionViewModelDesign { Address = "someaddress1", Amount = 1000, UtxoCount = 12, Path = "path", ViewRawJson = "json" },
            new TransactionViewModelDesign { Address = "someaddress2", Amount = 3000, UtxoCount = 15, Path = "path", ViewRawJson = "json" },
            new TransactionViewModelDesign { Address = "someaddress3", Amount = 43000, UtxoCount = 15, Path = "path", ViewRawJson = "json" },
            new TransactionViewModelDesign { Address = "someaddress4", Amount = 30000, UtxoCount = 15, Path = "path", ViewRawJson = "json" }];
    }
}