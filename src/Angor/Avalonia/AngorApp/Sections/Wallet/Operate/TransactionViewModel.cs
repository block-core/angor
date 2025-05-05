using AngorApp.UI.Services;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Wallet.Operate;

public class TransactionViewModel(IBroadcastedTransaction transaction, UIServices uiServices) : ITransactionViewModel
{
    public ReactiveCommand<Unit, Unit> ShowJson => ReactiveCommand.CreateFromTask(() => uiServices.Dialog.Show(new TransactionJsonViewModel(Transaction.RawJson), "Transaction Json", Observable.Return(true)));
    public IBroadcastedTransaction Transaction { get; } = transaction;
}