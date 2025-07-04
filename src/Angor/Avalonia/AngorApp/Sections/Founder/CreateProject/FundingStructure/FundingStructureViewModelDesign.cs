using System.Reactive.Disposables;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Founder.CreateProject.FundingStructure;

public partial class FundingStructureViewModelDesign : ReactiveValidationObject, IFundingStructureViewModel
{
    [ObservableAsProperty] private IAmountUI? targetAmount;
    [Reactive] private long? sats;
    private readonly CompositeDisposable disposable = new CompositeDisposable();
    public FundingStructureViewModelDesign()
    {
        this.ValidationRule(x => x.Sats, x => x is null or > 0, _ => "Amount must be greater than zero").DisposeWith(disposable);
        this.ValidationRule(x => x.Sats, x => x is not null, _ => "Please, specify an amount").DisposeWith(disposable);
        this.ValidationRule(x => x.EndDate, x => x != null, "Enter a date").DisposeWith(disposable);
        this.ValidationRule(x => x.ExpiryDate, x => x != null, "Enter a date").DisposeWith(disposable);
        this.ValidationRule(x => x.PenaltyDays, x => x >=0, "Should be greater than 0").DisposeWith(disposable);
        
        targetAmountHelper = this.WhenAnyValue(model => model.Sats)
            .WhereNotNull()
            .Select(l => new AmountUI(l.Value))
            .ToProperty(this, model => model.TargetAmount);
    }

    public IObservable<bool> IsValid { get; set; } = Observable.Return(true);

    public DateTime StartDate { get; set; }
    public int? PenaltyDays { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    
    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }
}