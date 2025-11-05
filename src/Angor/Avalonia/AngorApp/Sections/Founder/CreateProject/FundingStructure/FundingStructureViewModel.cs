using System.Reactive.Disposables;
using Angor.Shared;
using Blockcore.Networks;
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
    [ObservableAsProperty] private IAmountUI targetAmount;

    private readonly CompositeDisposable disposable = new();
    private readonly bool skipValidation;

    public FundingStructureViewModel(INetworkConfiguration networkConfiguration)
    {
        // Skip validation only if debug mode is enabled AND we're on testnet
        var isDebugMode = networkConfiguration.GetDebugMode();
        var network = networkConfiguration.GetNetwork();
        var isTestnet = network.NetworkType == NetworkType.Testnet;
        skipValidation = isDebugMode && isTestnet;

        // Always apply these validation rules
        this.ValidationRule(x => x.Sats, x => x is null or > 0, _ => "Amount must be greater than zero").DisposeWith(disposable);
        this.ValidationRule(x => x.Sats, x => x is not null, _ => "Amount should be especified").DisposeWith(disposable);
        this.ValidationRule(x => x.FundingEndDate, x => x != null, "Funding date needs to be specified").DisposeWith(disposable);
        this.ValidationRule(x => x.PenaltyDays, x => x >= 0, "Penalty Days should be greater than 0").DisposeWith(disposable);

        // Apply date validation unless we're skipping it
        if (!skipValidation)
        {
            this.ValidationRule(x => x.FundingEndDate, time => time >= FundingStartDate, "Funding end date should be after the funding start date").DisposeWith(disposable);
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