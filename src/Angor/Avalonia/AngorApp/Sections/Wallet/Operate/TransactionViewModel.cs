using AngorApp.UI.Services;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Wallet.Operate;

public interface ITransactionViewModel
{
    ReactiveCommand<Unit, Unit> ShowJson { get; }
    string Address { get; }
    long FeeRate { get; }
    long TotalFee { get; }
    long Amount { get; }
    string Path { get; }
    int UtxoCount { get; }
}

public class TransactionViewModel(IBroadcastedTransaction tx, UIServices uiServices) : ITransactionViewModel
{
    public ReactiveCommand<Unit, Unit> ShowJson { get; } = ReactiveCommand.CreateFromTask(() => uiServices.Dialog.ShowMessage("Transaction JSON", tx.ViewRawJson));
    public string Address => tx.Address;

    public long FeeRate => tx.FeeRate;

    public long TotalFee => tx.TotalFee;

    public long Amount => tx.Amount;

    public string Path => tx.Path;

    public int UtxoCount => tx.UtxoCount;
}