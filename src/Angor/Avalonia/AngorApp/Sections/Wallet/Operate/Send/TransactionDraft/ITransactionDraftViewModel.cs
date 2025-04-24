using Angor.Contexts.Wallet.Domain;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.Operate.Send.TransactionDraft;

public interface ITransactionDraftViewModel : IStep
{
    public ITransactionDraft TransactionDraft { get; }
    ReactiveCommand<Unit, Result<TxId>> Confirm { get; }
    ReactiveCommand<Unit, Result<ITransactionDraft>> CreateDraft { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    public SendAmount SendAmount { get; }
    public long Feerate { get; set; }
    public long Amount { get; }
}