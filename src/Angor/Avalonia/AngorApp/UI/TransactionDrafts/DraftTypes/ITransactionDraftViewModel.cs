using TransactionDraft = Angor.Contexts.Funding.Shared.TransactionDraft;

namespace AngorApp.UI.TransactionDrafts.DraftTypes;

public interface ITransactionDraftViewModel
{
    TransactionDraft Model { get; }
}