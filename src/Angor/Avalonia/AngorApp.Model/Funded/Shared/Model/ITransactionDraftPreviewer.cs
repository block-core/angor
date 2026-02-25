using Angor.Sdk.Funding.Shared;

namespace AngorApp.Model.Funded.Shared.Model;

public interface ITransactionDraftPreviewer
{
    Task<Result> PreviewAndCommit(
        Func<long, Task<Result<TransactionDraft>>> createDraft,
        Func<TransactionDraft, Task<Result<Guid>>> commitDraft,
        string title);
}
