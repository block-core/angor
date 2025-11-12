using TransactionDraft = Angor.Contexts.Funding.Shared.TransactionDraft;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.TransactionDrafts.DraftTypes.Base;

public class TransactionDraftViewModel(TransactionDraft dto, UIServices uiServices) : ITransactionDraftViewModel
{
    public TransactionDraft Model { get; } = dto;
    
    public IAmountUI TransactionFee => new AmountUI(dto.TransactionFee.Sats);
    
    public IEnhancedCommand ShowTransaction => ReactiveCommand.CreateFromTask(() =>
    {
        // TODO: Deserialize SignedTxHex
        var longTextViewModel = new LongTextViewModel { Text = dto.SignedTxHex };
        return uiServices.Dialog.Show(longTextViewModel, "Transaction", Observable.Return(true));
    }).Enhance();
}