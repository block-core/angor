using Angor.Contexts.Wallet.Domain;
using AngorApp.UI.Controls;

namespace AngorApp.Sections.Wallet.Operate.Send.TransactionDraft;

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
    public ReactiveCommand<Unit, Result<TxId>> Confirm => ReactiveCommand.Create(() => Result.Success(default(TxId)));
    public ReactiveCommand<Unit, Result<ITransactionDraft>> CreateDraft { get; } = ReactiveCommand.Create(() => Result.Success(default(ITransactionDraft)));
    public IObservable<bool> TransactionConfirmed { get; } = Observable.Return(false);
    public SendAmount SendAmount { get; } = new("Sample Destination", 1000, "mzHrLAR3WWLE4eCpq82BDCKmLeYRyYXPtm");
    public long? Sats { get; set; } = 1;
    public long Amount { get; } = 1000;

    public IEnumerable<IFeeratePreset> Presets
    {
        get
        {
            return new[]
            {
                new Preset("Economy", new AmountUI(2), null, null),
                new Preset("Standard", new AmountUI(12), null, null),
                new Preset("Priority", new AmountUI(20), null, null),
            };
        }
    }

    public IObservable<bool> IsValid { get; } = Observable.Return(true);
    public bool AutoAdvance => false;
}