using TransactionDraft = Angor.Sdk.Funding.Shared.TransactionDraft;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.TransactionDrafts.DraftTypes.Base;

public class TransactionDraftViewModel(TransactionDraft dto, UIServices uiServices) : ITransactionDraftViewModel
{
    public TransactionDraft Model { get; } = dto;
    
    public IAmountUI TransactionFee => new AmountUI(Model.TransactionFee.Sats);
    
    public IEnhancedCommand ShowTransaction => ReactiveCommand.CreateFromTask(() =>
    {
        // TODO: Deserialize SignedTxHex
        var longTextViewModel = new LongTextViewModel { Text = Model.SignedTxHex };
        return uiServices.Dialog.Show(longTextViewModel, "Transaction", Observable.Return(true));
    }).Enhance();
}