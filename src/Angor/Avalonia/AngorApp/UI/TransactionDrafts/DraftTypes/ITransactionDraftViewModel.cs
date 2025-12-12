using TransactionDraft = Angor.Sdk.Funding.Shared.TransactionDraft;

namespace AngorApp.UI.TransactionDrafts.DraftTypes;

public interface ITransactionDraftViewModel
{
    TransactionDraft Model { get; }
}