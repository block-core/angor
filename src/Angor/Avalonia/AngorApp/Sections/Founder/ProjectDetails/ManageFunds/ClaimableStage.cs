using System.Linq;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.UI.Services;
using Avalonia.Controls.Selection;
using Zafiro.Avalonia.Misc;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

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
        
        Claim = ReactiveCommand.CreateFromTask(() => DoClaim(ReactiveSelection.SelectedItems), selectedCountChanged.Select(i => i > 0)).Enhance();
        Claim.HandleErrorsWith(uiServices.NotificationService, "Error claiming transactions");
        
        Transactions = transactions;
        ClaimableTransactionsCount = transactions.Count(transaction => transaction.IsClaimable);
        var sats = transactions.Sum(transaction => transaction.Amount.Sats);
        ClaimableAmount = new AmountUI(sats);
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

    public IEnhancedCommand<Result> Claim { get; }
}