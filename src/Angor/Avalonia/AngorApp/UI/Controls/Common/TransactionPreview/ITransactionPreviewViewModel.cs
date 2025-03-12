using Angor.Wallet.Domain;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.UI.Controls.Common.TransactionPreview;

public interface ITransactionPreviewViewModel : IStep
{
    public ITransactionPreview TransactionPreview { get; }
    ReactiveCommand<Unit, Result<TxId>> Confirm { get; }
    ReactiveCommand<Unit, Result<ITransactionPreview>> CreatePreview { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    public Destination Destination { get; }
    public long Feerate { get; set; }
}