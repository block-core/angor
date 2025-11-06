using TransactionDraft = Angor.Contexts.Funding.Shared.TransactionDraft;

namespace AngorApp.TransactionDrafts.DraftTypes;

public interface ITransactionDraftViewModel
{
    TransactionDraft Model { get; }
}