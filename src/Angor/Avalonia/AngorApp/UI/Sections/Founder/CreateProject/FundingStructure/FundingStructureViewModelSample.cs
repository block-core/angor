using System.Reactive.Disposables;
using System.Linq;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Angor.Shared.Models;
using System.Collections.ObjectModel;
using Angor.Sdk.Funding.Founder.Dtos;

namespace AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;

public partial class FundingStructureViewModelSample : ReactiveValidationObject, IFundingStructureViewModel
{
    [ObservableAsProperty] private IAmountUI? targetAmount;
    [ObservableAsProperty] private ICollection<string>? errors;
    [Reactive] private long? sats;
    private readonly CompositeDisposable disposable = new CompositeDisposable();

    public FundingStructureViewModelSample()
    {
        SelectableDynamicStagePatterns = new ObservableCollection<SelectableDynamicStagePattern>();
        SelectedPatterns = new ObservableCollection<DynamicStagePattern>();

        this.ValidationRule(x => x.Sats, x => x is null or > 0, _ => "Amount must be greater than zero").DisposeWith(disposable);
        this.ValidationRule(x => x.Sats, x => x is not null, _ => "Please, specify an amount").DisposeWith(disposable);
        this.ValidationRule(x => x.FundingEndDate, x => x != null, "Enter a date").DisposeWith(disposable);
        this.ValidationRule(x => x.ExpiryDate, x => x != null, "Enter a date").DisposeWith(disposable);
        this.ValidationRule(x => x.PenaltyDays, x => x >= 0, "Should be greater than 0").DisposeWith(disposable);
        this.ValidationRule(x => x.PenaltyThreshold, x => x is null or >= 0, "Should be greater than or equal to 0").DisposeWith(disposable);

        targetAmountHelper = this.WhenAnyValue(model => model.Sats)
            .WhereNotNull()
            .Select(l => new AmountUI(l.Value))
            .ToProperty(this, model => model.TargetAmount);

        errorsHelper = this.ValidationContext.ValidationStatusChange
            .Select(state => state.Text.ToList())
            .StartWith(this.ValidationContext.Text.ToList())
            .ToProperty(this, model => model.Errors);
    }

    public IObservable<bool> IsValid { get; set; } = Observable.Return(true);
    public ProjectType ProjectType { get; set; } = ProjectType.Invest;
    public DateTime FundingStartDate { get; set; }
    public int? PenaltyDays { get; set; }
    public long? PenaltyThreshold { get; set; }
    public DateTime? FundingEndDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public ObservableCollection<SelectableDynamicStagePattern> SelectableDynamicStagePatterns { get; }
    public ObservableCollection<DynamicStagePattern> SelectedPatterns { get; }
    public int? PayoutDay { get; set; }
    public List<DynamicStagePattern> DynamicStagePatterns => SelectableDynamicStagePatterns.Select(s => s.Pattern).ToList();

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }

    public void ApplyMoonshotData(MoonshotProjectData moonshotData)
    {
        // No-op for sample
    }
}
