using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Collections;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.Reactive;

namespace AngorApp.Sections.Founder.CreateProject.FundingStructure;

public partial class FundingStructureViewModel : ReactiveValidationObject, IFundingStructureViewModel
{
    [Reactive] private long? sats;
    [Reactive] private int? penaltyDays = 100;
    [Reactive] private DateTime? fundingEndDate;
    [Reactive] private DateTime? expiryDate;
    [ObservableAsProperty] private IAmountUI targetAmount;
    [ObservableAsProperty] private IEnumerable<string>? errors;

    private readonly CompositeDisposable disposable = new();

    public FundingStructureViewModel()
    {
        this.ValidationRule(x => x.Sats, x => x is null or > 0, _ => "Amount must be greater than zero").DisposeWith(disposable);
        this.ValidationRule(x => x.Sats, x => x is not null, _ => "Amount should be especified").DisposeWith(disposable);
        this.ValidationRule(x => x.FundingEndDate, x => x != null, "Funding date needs to be specified").DisposeWith(disposable);
        this.ValidationRule(x => x.ExpiryDate, x => x != null, "Expiry date should not be empty").DisposeWith(disposable);
        this.ValidationRule(x => x.PenaltyDays, x => x >= 0, "Penalty Days should be greater than 0").DisposeWith(disposable);
        this.ValidationRule(x => x.FundingEndDate, time => time >= FundingStartDate, "Funding end date should be after the funding start date").DisposeWith(disposable);

        targetAmountHelper = this.WhenAnyValue(x => x.Sats)
            .WhereNotNull()
            .Select(l => new AmountUI(l.Value))
            .ToProperty(this, model => model.TargetAmount)
            .DisposeWith(disposable);

        errorsHelper = this.ValidationContext.ValidationStatusChange
            .Where(state => !state.IsValid)
            .Select(state => state.Text.ToList())
            .ToProperty(this, model => model.Errors)
            .DisposeWith(disposable);
    }

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }

    public IObservable<bool> IsValid => this.IsValid();
    public DateTime FundingStartDate { get; } = DateTime.Now;
}
