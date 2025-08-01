using AngorApp.UI.Controls;
using AngorApp.UI.Controls.Feerate;

namespace AngorApp.Sections.Wallet.Operate.Send.TransactionDraft;

public interface ITransactionDraftViewModel
{
    public long? Feerate { get; set; }
    public IEnumerable<IFeeratePreset> Presets { get; }
    public IAmountUI? Fee { get; }
    public IObservable<bool> IsSending { get; }
    public IObservable<bool> IsCalculating { get; }
}