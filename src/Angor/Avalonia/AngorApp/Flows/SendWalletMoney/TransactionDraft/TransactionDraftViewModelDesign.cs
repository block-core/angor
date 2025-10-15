using Angor.Contexts.Wallet.Domain;
using AngorApp.UI.Controls.Feerate;
using Preset = AngorApp.UI.Controls.Feerate.Preset;

namespace AngorApp.Flows.SendWalletMoney.TransactionDraft;

public class TransactionDraftViewModelDesign : ITransactionDraftViewModel
{
    public ReactiveCommand<Unit, Result<TxId>> Confirm => ReactiveCommand.Create(() => Result.Success(default(TxId)));
    public long? Feerate { get; set; }

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

    public IAmountUI? Fee { get; }
    public IObservable<bool> IsSending { get; set; }
    public IObservable<bool> IsCalculating { get; set; }
}