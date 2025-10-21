using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Shared;
using AngorApp.UI.Controls.Common;
using AngorApp.UI.Services;
using Avalonia.Controls.Selection;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Misc;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Mixins;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.Claim;

public class ClaimableStage : ReactiveObject, IClaimableStage
{
    private readonly IFullProject project;
    private readonly int stageId;
    private readonly IFounderAppService founderAppService;
    private readonly IWalletContext walletContext;

    public ClaimableStage(IFullProject project, int stageId, ICollection<IClaimableTransaction> transactions, IFounderAppService founderAppService, UIServices uiServices, IWalletContext walletContext)
    {
        this.project = project;
        this.stageId = stageId;
        this.founderAppService = founderAppService;
        this.walletContext = walletContext;

        ReactiveSelection = new ReactiveSelection<IClaimableTransaction, string>(new SelectionModel<IClaimableTransaction>
        {
            SingleSelect = false
        }, x => x.Address, transaction => transaction.IsClaimable);

        var selectedCountChanged = this.WhenAnyValue(design => design.ReactiveSelection.SelectedItems.Count);

        Claim = ReactiveCommand.CreateFromTask(() => ClaimSelectedTransactions(uiServices), selectedCountChanged.Select(i => i > 0))
            .Enhance();

        Claim.Values().HandleErrorsWith(uiServices.NotificationService, "Error claiming transactions");

        Transactions = transactions;
        ClaimableTransactionsCount = transactions.Count(transaction => transaction.IsClaimable);
        var sats = transactions.Sum(transaction => transaction.Amount.Sats);
        ClaimableAmount = new AmountUI(sats);
    }

    private Task<Maybe<Result>> ClaimSelectedTransactions(UIServices uiServices)
    {
        var feerate = uiServices.Dialog.ShowAndGetResult(new FeerateSelectionViewModel(uiServices), "Select your feerate", model => model.IsValid, model => model.Feerate.Value);

        return feerate
            .Map(fr => walletContext.RequiresWallet(wallet => DoClaim(ReactiveSelection.SelectedItems, wallet.Id.Value, fr)
                .Tap(() => uiServices.Dialog.ShowMessage("Claim successful", "The funds have been successfully claimed.", "Close"))));
    }

    private async Task<Result> DoClaim(IEnumerable<IClaimableTransaction> selected, Guid walletId, long feerate)
    {
        var toSpend = selected.Select(claimable => new SpendTransactionDto
        {
            InvestorAddress = claimable.Address,
            StageId = stageId
        });

        var result = await founderAppService.Spend(walletId, new DomainFeerate(feerate), project.ProjectId, toSpend);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }

    public ReactiveSelection<IClaimableTransaction, string> ReactiveSelection { get; }


    public int ClaimableTransactionsCount { get; }

    public IEnumerable<IClaimableTransaction> Transactions { get; }

    public IAmountUI ClaimableAmount { get; }

    public IEnhancedCommand<Maybe<Result>> Claim { get; }
}
