using AngorApp.Model.Funded.Shared.Model;
using AngorApp.UI.TransactionDrafts.DraftTypes;
using AngorApp.UI.TransactionDrafts.DraftTypes.Base;
using Zafiro.Avalonia.Dialogs;
using TransactionDraft = Angor.Sdk.Funding.Shared.TransactionDraft;

namespace AngorApp.UI.TransactionDrafts;

public class TransactionDraftPreviewer(IDialog dialog, UIServices uiServices) : ITransactionDraftPreviewer
{
    public async Task<Result> PreviewAndCommit(
        Func<long, Task<Result<TransactionDraft>>> createDraft,
        Func<TransactionDraft, Task<Result<Guid>>> commitDraft,
        string title)
    {
        Func<long, Task<Result<ITransactionDraftViewModel>>> getDraft = feerate =>
            createDraft(feerate).Map(ITransactionDraftViewModel (draft) => new TransactionDraftViewModel(draft, uiServices));

        Func<ITransactionDraftViewModel, Task<Result<Guid>>> commit = model =>
            commitDraft(model.Model);

        var previewer = new TransactionDraftPreviewerViewModel(getDraft, commit, uiServices);
        await dialog.ShowAndGetResult(previewer, title, s => s.CommitDraft.Enhance(title));

        return Result.Success();
    }
}
