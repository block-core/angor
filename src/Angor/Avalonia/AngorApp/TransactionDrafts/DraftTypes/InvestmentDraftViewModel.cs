using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Zafiro.Avalonia.Dialogs;
using TransactionDraft = Angor.Contexts.Funding.Shared.TransactionDraft;

namespace AngorApp.TransactionDrafts.DraftTypes;

public interface ITransactionDraftViewModel
{
    TransactionDraft Model { get; }
}

public class InvestmentTransactionDraftViewModel(InvestmentDraft dto, UIServices uiServices) : ITransactionDraftViewModel
{
    public IAmountUI AngorFee => new AmountUI(dto.AngorFee.Sats);
    public IAmountUI MinerFee => new AmountUI(dto.MinerFee.Sats);
    public IAmountUI TransactionFee => new AmountUI(dto.TransactionFee.Sats);
    public string SignedTxHex => dto.SignedTxHex;
    public TransactionDraft Model => dto;
    public IEnhancedCommand ShowTransaction => ReactiveCommand.CreateFromTask(() =>
    {
        // TODO: Deserialize SignedTxHex
        var longTextViewModel = new LongTextViewModel { Text = dto.SignedTxHex };
        return uiServices.Dialog.Show(longTextViewModel, "Transaction", Observable.Return(true));
    }).Enhance();
}