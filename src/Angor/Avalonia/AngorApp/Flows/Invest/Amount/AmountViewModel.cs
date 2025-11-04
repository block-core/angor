using System.Linq;
using AngorApp.Model.Projects;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Flows.Invest.Amount;

public partial class AmountViewModel : ReactiveValidationObject, IAmountViewModel, IValidatable
{
    [Reactive] private long? amount;
    [ObservableAsProperty] private IEnumerable<Breakdown> stageBreakdowns;
    [ObservableAsProperty] private bool requiresFounderApproval;
    
    public AmountViewModel(IWallet wallet, FullProject project)
    {
        this.ValidationRule(x => x.Amount, x => x is null or > 0, _ => "Amount must be greater than zero");
        this.ValidationRule(x => x.Amount, x => x is not null, _ => "Please, specify an amount");
        
        var isValidAmount = this
            .WhenAnyValue(x => x.Amount)
            .WithLatestFrom(wallet.WhenAnyValue(x => x.Balance), (amount, walletBalance) => amount is null || amount <= walletBalance.Sats);
        
        //this.ValidationRule(x => x.Amount, isValidAmount, "Amount exceeds balance");

        stageBreakdownsHelper = this.WhenAnyValue(model => model.Amount)
            .WhereNotNull()
            .Select(investAmount => project.Stages.Select(stage => new Breakdown(stage.Index, new AmountUI(investAmount!.Value), stage.RatioOfTotal, stage.ReleaseDate)))
            .ToProperty(this, x => x.StageBreakdowns);

        requiresFounderApprovalHelper = this.WhenAnyValue(model => model.Amount)
            .Select(investAmount =>
            {
                if (investAmount == null || project.PenaltyThreshold == null)
                    return false;
                
                // Requires approval if investment is AT OR ABOVE the threshold
                // Below threshold = no penalty, no approval needed
                return investAmount.Value > project.PenaltyThreshold.Sats;
            })
            .ToProperty(this, x => x.RequiresFounderApproval);
    }

    public IObservable<bool> IsValid => this.IsValid();
}