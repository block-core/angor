using System.Linq;
using Angor.Shared.Models;
using AngorApp.Model.Projects;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Flows.Invest.Amount;

public partial class AmountViewModel : ReactiveValidationObject, IAmountViewModel, IValidatable
{
    [Reactive] private long? amount;
    [Reactive] private byte? selectedPatternIndex;
    [ObservableAsProperty] private IEnumerable<Breakdown> stageBreakdowns;
    [ObservableAsProperty] private bool requiresFounderApproval;
    [ObservableAsProperty] private bool requiresPatternSelection;

    private readonly FullProject project;

    public AmountViewModel(IWallet wallet, FullProject project)
    {
        this.project = project;

        this.ValidationRule(x => x.Amount, x => x is null or > 0, _ => "Amount must be greater than zero");
        this.ValidationRule(x => x.Amount, x => x is not null, _ => "Please, specify an amount");

        var isValidAmount = this
            .WhenAnyValue(x => x.Amount)
            .WithLatestFrom(wallet.WhenAnyValue(x => x.Balance), (amount, walletBalance) => amount is null || amount <= walletBalance.Sats);

        //this.ValidationRule(x => x.Amount, isValidAmount, "Amount exceeds balance");

        // Pattern selection validation for Fund/Subscribe projects
        var needsPattern = project.ProjectType == ProjectType.Fund || project.ProjectType == ProjectType.Subscribe;

        requiresPatternSelectionHelper = Observable.Return(needsPattern).ToProperty(this, x => x.RequiresPatternSelection);

        if (needsPattern)
        {
            this.ValidationRule(x => x.SelectedPatternIndex, x => x.HasValue, _ => "Please select a funding pattern");
        }

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

    // Expose available patterns for UI binding
    public List<DynamicStagePattern> AvailablePatterns => project.DynamicStagePatterns;

    // Expose project type to conditionally show/hide pattern selector
    public ProjectType ProjectType => project.ProjectType;
}