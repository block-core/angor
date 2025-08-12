using System.Reactive.Disposables;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Founder.CreateProject.FundingStructure;

public partial class FundingStructureViewModel : ReactiveValidationObject, IFundingStructureViewModel
{
    [Reactive] private long? sats;
    [Reactive] private int? penaltyDays = 60;
    [Reactive] private DateTime? endDate;
    [Reactive] private DateTime? expiryDate;
    [ObservableAsProperty] private IAmountUI targetAmount;
    
    private readonly CompositeDisposable disposable = new();

    public FundingStructureViewModel()
    {
#if DEBUG
        ExpiryDate = DateTime.Now; 
        EndDate = DateTime.Now;
        Sats = 1000000; // 10,000,000 Sats
#endif
        
        this.ValidationRule(x => x.Sats, x => x is null or > 0, _ => "Amount must be greater than zero").DisposeWith(disposable);
        this.ValidationRule(x => x.Sats, x => x is not null, _ => "Please, specify an amount").DisposeWith(disposable);
        this.ValidationRule(x => x.EndDate, x => x != null, "Enter a date").DisposeWith(disposable);
        this.ValidationRule(x => x.ExpiryDate, x => x != null, "Enter a date").DisposeWith(disposable);
        this.ValidationRule(x => x.PenaltyDays, x => x >=0, "Should be greater than 0").DisposeWith(disposable);
        
        targetAmountHelper = this.WhenAnyValue(x => x.Sats)
            .WhereNotNull()
            .Select(l => new AmountUI(l.Value))
            .ToProperty(this, model => model.TargetAmount)
            .DisposeWith(disposable);
    }

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }

    public IObservable<bool> IsValid => this.IsValid();
    public DateTime StartDate { get; } = DateTime.Now;
}