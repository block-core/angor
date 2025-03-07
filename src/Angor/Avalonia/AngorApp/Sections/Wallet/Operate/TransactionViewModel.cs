using AngorApp.UI.Services;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Wallet.Operate;

public class TransactionViewModel(IBroadcastedTransaction transaction,UIServices uiServices)
{
    public string Address => transaction.Address;
    public long Amount => transaction.Amount;
    public int UtxoCount => transaction.UtxoCount;
    public ReactiveCommand<Unit, Unit> ShowJson => ReactiveCommand.CreateFromTask(() => uiServices.Dialog.Show(new TransactionJsonViewModel(transaction.ViewRawJson), "Transaction Json", Observable.Return(true)));
}