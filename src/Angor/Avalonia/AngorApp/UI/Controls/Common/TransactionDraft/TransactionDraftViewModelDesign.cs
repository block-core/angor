using Angor.Contexts.Wallet.Domain;
using AngorApp.Sections.Wallet.Operate;

namespace AngorApp.UI.Controls.Common.TransactionDraft;

public class TransactionDraftViewModelDesign : ITransactionDraftViewModel
{
    public ITransactionDraft TransactionDraft { get; set; } = new TransactionDraftDesign
    {
        TotalFee = 10,
        Address = "Test Address",
        Amount = 200,
        Path = "PATH",
        FeeRate = 12,
        UtxoCount = 1,
        ViewRawJson = "JSON"
    };

    public IObservable<bool> IsBusy { get; set; } = Observable.Return(false);
    public ReactiveCommand<Unit, Result<TxId>> Confirm { get; }
    public ReactiveCommand<Unit, Result<ITransactionDraft>> CreateDraft { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    public Destination Destination { get; } = new("Sample Destination", 1000, "mzHrLAR3WWLE4eCpq82BDCKmLeYRyYXPtm");
    public long Feerate { get; set; } = 1;
    public IObservable<bool> IsValid { get; }
    public bool AutoAdvance => false;
}