using ReactiveUI.SourceGenerators;

namespace AngorApp.Sections.Founder.CreateProject.FundingStructure;

public partial class FundingStructureViewModelDesign : ReactiveObject, IFundingStructureViewModel
{
    [ObservableAsProperty] private IAmountUI? targetAmount;
    [Reactive] private long? sats; 
    public FundingStructureViewModelDesign()
    {
        targetAmountHelper = this.WhenAnyValue(model => model.Sats)
            .WhereNotNull()
            .Select(l => new AmountUI(l.Value))
            .ToProperty(this, model => model.TargetAmount);
    }
    
    public IObservable<bool> IsValid { get; set; }

    public DateTime StartDate { get; set; }
    public int? PenaltyDays { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}