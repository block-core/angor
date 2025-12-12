using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.TransactionDrafts;
using AngorApp.UI.TransactionDrafts.DraftTypes;
using AngorApp.UI.TransactionDrafts.DraftTypes.Base;
using Avalonia.Controls.Selection;
using System.Linq;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Misc;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Claim;

public class ClaimableStage : ReactiveObject, IClaimableStage
{
    private readonly ProjectId projectId;
    private readonly int stageId;
    private readonly IFounderAppService founderAppService;
    private readonly IWalletContext walletContext;

    public ClaimableStage(ProjectId projectId, int stageId, ICollection<IClaimableTransaction> transactions, IFounderAppService founderAppService, UIServices uiServices, IWalletContext walletContext)
    {
        this.projectId = projectId;
        this.stageId = stageId;
        this.founderAppService = founderAppService;
        this.walletContext = walletContext;

        ReactiveSelection = new ReactiveSelection<IClaimableTransaction, string>(new SelectionModel<IClaimableTransaction>
        {
            SingleSelect = false
        }, x => x.Address, transaction => transaction.IsClaimable);

        var selectedCountChanged = this.WhenAnyValue(design => design.ReactiveSelection.SelectedItems.Count);

        Claim = ReactiveCommand.CreateFromTask(() => ClaimSelectedTransactions(uiServices)).Enhance();

        Claim.Values().HandleErrorsWith(uiServices.NotificationService, "Error claiming transactions");

        Transactions = transactions;
        ClaimableTransactionsCount = transactions.Count(transaction => transaction.IsClaimable);
        var sats = transactions.Sum(transaction => transaction.Amount.Sats);
        ClaimableAmount = new AmountUI(sats);
    }

    private async Task<Maybe<Result>> ClaimSelectedTransactions(UIServices uiServices)
    {
        var toSpend = ReactiveSelection.SelectedItems.Select(claimable => new SpendTransactionDto
        {
            InvestorAddress = claimable.Address,
            StageId = stageId
        });

        var wallet = walletContext.CurrentWallet;

        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(
          feerate =>
          {
              return founderAppService.Spend(wallet.Value.Id, new DomainFeerate(feerate), projectId, toSpend)
                .Map(ITransactionDraftViewModel (draft) => new TransactionDraftViewModel(draft, uiServices));
          },
          model =>
          {
              return founderAppService.SubmitTransactionFromDraft(wallet.Value.Id, model.Model)
               .Tap(_ => uiServices.Dialog.ShowOk("Success", "Founder claim transaction submitted successfully"))
               .Map(_ => Guid.Empty);
          },
          uiServices);

        var dialogRes = await uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Claim Funds", s => s.CommitDraft.Enhance("Claim Funds"));

        return dialogRes.Map(_ => Result.Success());
    }

    public ReactiveSelection<IClaimableTransaction, string> ReactiveSelection { get; }

    public int ClaimableTransactionsCount { get; }

    public IEnumerable<IClaimableTransaction> Transactions { get; }

    public IAmountUI ClaimableAmount { get; }

    public IEnhancedCommand<Maybe<Result>> Claim { get; }
}
