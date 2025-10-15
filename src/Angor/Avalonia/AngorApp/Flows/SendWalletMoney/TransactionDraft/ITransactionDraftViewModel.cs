using AngorApp.UI.Controls.Feerate;

namespace AngorApp.Flows.SendWalletMoney.TransactionDraft;

public interface ITransactionDraftViewModel
{
    public long? Feerate { get; set; }
    public IEnumerable<IFeeratePreset> Presets { get; }
    public IAmountUI? Fee { get; }
    public IObservable<bool> IsSending { get; }
    public IObservable<bool> IsCalculating { get; }
}