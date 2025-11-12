using System.Reactive.Disposables;
using Angor.Shared;
using Blockcore.Networks;
using NBitcoin;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;

public partial class FundingStructureViewModel : ReactiveValidationObject, IFundingStructureViewModel, IHaveErrors
{
    [Reactive] private long? sats;
    [Reactive] private int? penaltyDays = 100;
    [Reactive] private long? penaltyThreshold;
    [Reactive] private DateTime? fundingEndDate;
    [Reactive] private DateTime? expiryDate;
    [ObservableAsProperty] private IAmountUI targetAmount;

    private readonly CompositeDisposable disposable = new();
    private readonly bool skipValidation;

    public FundingStructureViewModel(INetworkConfiguration networkConfiguration)
    {
        // Skip production validations only if debug mode is enabled AND we're on testnet
        var isDebugMode = networkConfiguration.GetDebugMode();
        var network = networkConfiguration.GetNetwork();
        var isTestnet = network.NetworkType == NetworkType.Testnet;
        skipValidation = isDebugMode && isTestnet;

        // TARGET AMOUNT VALIDATIONS
        // Always enforced: Must be > 0
        this.ValidationRule(x => x.Sats, x => x is not null, _ => "Target amount is required.").DisposeWith(disposable);
        this.ValidationRule(x => x.Sats, x => x is null or > 0, _ => "Target amount must be greater than 0.").DisposeWith(disposable);

        // Production only: Min 0.001 BTC (100,000 sats), Max 100 BTC (10,000,000,000 sats)
        if (!skipValidation)
        {
            this.ValidationRule(x => x.Sats, x => x is null || x >= 100_000, _ => "Target amount must be at least 0.001 BTC.").DisposeWith(disposable);
            this.ValidationRule(x => x.Sats, x => x is null || x <= 10_000_000_000, _ => "Target amount cannot exceed 100 BTC.").DisposeWith(disposable);
        }

        // PENALTY DAYS VALIDATIONS
        // Always enforced: Cannot be negative, Max 365
        this.ValidationRule(x => x.PenaltyDays, x => x >= 0, "Penalty days cannot be negative.").DisposeWith(disposable);
        this.ValidationRule(x => x.PenaltyDays, x => x is null || x <= 365, "Penalty period cannot exceed 365 days.").DisposeWith(disposable);

        // Production only: Min 10 days
        if (!skipValidation)
        {
            this.ValidationRule(x => x.PenaltyDays, x => x is null || x >= 10, "Penalty period must be at least 10 days.").DisposeWith(disposable);
        }

        // PENALTY THRESHOLD VALIDATION (Always)
        this.ValidationRule(x => x.PenaltyThreshold, x => x is null or >= 0, "Penalty threshold must be greater than or equal to 0.").DisposeWith(disposable);

        // DATE VALIDATIONS
        // Always enforced: Funding end date must be specified
        this.ValidationRule(x => x.FundingEndDate, x => x != null, "Funding date needs to be specified.").DisposeWith(disposable);

        // Date comparison validation (Debug allows equal dates, Production requires strictly after)
        if (skipValidation)
        {
            // Debug mode: Allow end date to be equal to or after start date
            this.ValidationRule(x => x.FundingEndDate, time => time >= FundingStartDate, "Funding end date must be on or after the funding start date.").DisposeWith(disposable);
        }
        else
        {
            // Production mode: End date must be strictly after start date
            this.ValidationRule(x => x.FundingEndDate, time => time > FundingStartDate, "Funding end date must be after the start date.").DisposeWith(disposable);

            // Production only: Funding period cannot exceed 1 year
            this.ValidationRule(x => x.FundingEndDate,
                time => time == null || (time.Value - FundingStartDate).TotalDays <= 365,
                "Funding period cannot exceed one year.").DisposeWith(disposable);
        }

        targetAmountHelper = this.WhenAnyValue(x => x.Sats)
            .WhereNotNull()
            .Select(l => new AmountUI(l.Value))
            .ToProperty(this, model => model.TargetAmount)
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