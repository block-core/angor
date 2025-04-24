using Angor.Contexts.Wallet.Domain;
using AngorApp.UI.Controls;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.Operate.Send.TransactionDraft;

public interface ITransactionDraftViewModel : IStep
{
    ReactiveCommand<Unit, Result<TxId>> Confirm { get; }
    public long? Sats { get; set; }
    public IEnumerable<IFeeratePreset> Presets { get; }
}