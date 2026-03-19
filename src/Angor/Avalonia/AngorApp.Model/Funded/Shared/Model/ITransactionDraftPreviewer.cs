using Angor.Sdk.Common;
using Angor.Sdk.Funding.Shared;

namespace AngorApp.Model.Funded.Shared.Model;

public interface ITransactionDraftPreviewer
{
    Task<Maybe<Result>> PreviewAndCommit(
        Func<long, Task<Result<TransactionDraft>>> createDraft,
        Func<TransactionDraft, Task<Result<Guid>>> commitDraft,
        string title,
        WalletId? walletId = null);
}
