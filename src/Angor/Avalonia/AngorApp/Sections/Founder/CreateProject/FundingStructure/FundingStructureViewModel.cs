using System.Reactive.Disposables;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Founder.CreateProject.FundingStructure;

public partial class FundingStructureViewModel : ReactiveValidationObject, IFundingStructureViewModel, IHaveErrors
{
    [Reactive] private long? sats;
    [Reactive] private int? penaltyDays = 100;
    [Reactive] private DateTime? fundingEndDate;
    [Reactive] private DateTime? expiryDate;
    [Reactive] private long? penaltyThresholdSats;
    [Reactive] private bool? enforceTargetAmount;
    [ObservableAsProperty] private IAmountUI targetAmount;
    [ObservableAsProperty] private IAmountUI penaltyThreshold;

    private readonly CompositeDisposable disposable = new();

    public FundingStructureViewModel()
    {
        this.ValidationRule(x => x.Sats, x => x is null or > 0, _ => "Amount must be greater than zero").DisposeWith(disposable);
        this.ValidationRule(x => x.Sats, x => x is not null, _ => "Amount should be specified").DisposeWith(disposable);
        this.ValidationRule(x => x.FundingEndDate, x => x != null, "Funding date needs to be specified").DisposeWith(disposable);
        this.ValidationRule(x => x.PenaltyDays, x => x >= 0, "Penalty Days should be greater than 0").DisposeWith(disposable);
        this.ValidationRule(x => x.FundingEndDate, time => time >= FundingStartDate, "Funding end date should be after the funding start date").DisposeWith(disposable);
        this.ValidationRule(x => x.PenaltyThresholdSats, x => x is null or > 0, _ => "Penalty threshold must be greater than zero").DisposeWith(disposable);

        targetAmountHelper = this.WhenAnyValue(x => x.Sats)
            .WhereNotNull()
            .Select(l => new AmountUI(l.Value))
            .ToProperty(this, model => model.TargetAmount)
            .DisposeWith(disposable);

        penaltyThresholdHelper = this.WhenAnyValue(x => x.PenaltyThresholdSats)
            .WhereNotNull()
            .Select(l => new AmountUI(l.Value))
            .ToProperty(this, model => model.PenaltyThreshold)
            .DisposeWith(disposable);

        Errors = new ErrorSummarizer(ValidationContext).DisposeWith(disposable).Errors;
    }

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }

    public IObservable<bool> IsValid => this.IsValid();
    public DateTime FundingStartDate { get; } = DateTime.Now;
    public ICollection<string> Errors { get; }
}