using System.Linq;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
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
    private readonly ProjectId projectId;
    private readonly int stageId;
    private readonly IInvestmentAppService investmentAppService;

    public ClaimableStage(ProjectId projectId, int stageId, ICollection<IClaimableTransaction> transactions, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        this.projectId = projectId;
        this.stageId = stageId;
        this.investmentAppService = investmentAppService;

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
        var transactions = ReactiveSelection.SelectedItems.Select(x => $"· {x.Amount.DecimalString} from {x.Address}").JoinWithLines();
        return uiServices.Dialog.ShowConfirmation("Claim funds?", "Proceed to claim the funds of the selected transactions?\n\n" + transactions)
            .Where(confirmed => confirmed)
            .Map(_ => DoClaim(ReactiveSelection.SelectedItems));
    }

    private Task<Result> DoClaim(IEnumerable<IClaimableTransaction> selected)
    {
        var spends = selected.Select(claimable => new SpendTransactionDto
        {
            InvestorAddress = claimable.Address,
            StageId = stageId
        });

        return investmentAppService.Spend(projectId, spends);
    }

    public ReactiveSelection<IClaimableTransaction, string> ReactiveSelection { get; }


    public int ClaimableTransactionsCount { get; }

    public IEnumerable<IClaimableTransaction> Transactions { get; }

    public IAmountUI ClaimableAmount { get; }

    public IEnhancedCommand<Maybe<Result>> Claim { get; }
}