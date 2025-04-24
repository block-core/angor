using Angor.Contexts.Wallet.Domain;
using AngorApp.UI.Controls;

namespace AngorApp.Sections.Wallet.Operate.Send.TransactionDraft;

public class TransactionDraftViewModelDesign : ITransactionDraftViewModel
{
    public IObservable<bool> IsBusy { get; } = Observable.Return(false);
    public ReactiveCommand<Unit, Result<TxId>> Confirm => ReactiveCommand.Create(() => Result.Success(default(TxId)));
    public long? Feerate { get; set; }
    public long? Sats { get; set; } = 1;

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