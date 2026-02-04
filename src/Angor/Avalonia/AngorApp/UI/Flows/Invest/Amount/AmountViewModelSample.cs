using Angor.Shared.Models;

namespace AngorApp.UI.Flows.Invest.Amount;

public class AmountViewModelSample : IAmountViewModel
{
    public long? Amount { get; set; } = 20000;

    public IEnumerable<Breakdown> StageBreakdowns { get; } = new List<Breakdown>
    {
        new(new AmountUI(120), 0.2m, DateTime.Now),
        new(new AmountUI(120), 0.4m, DateTime.Now.AddMonths(1)),
        new(new AmountUI(120), 0.6m, DateTime.Now.AddMonths(2).AddDays(5)),
    };

    public IObservable<bool> IsValid { get; } = Observable.Return(true);
    public bool RequiresFounderApproval { get; } = true; // Show penalty warning in design mode (above threshold scenario)

    // New properties for Fund/Subscribe support
    public byte? SelectedPatternIndex { get; set; } = 0;
    public bool RequiresPatternSelection { get; } = false; // Set to true to test pattern selection UI
    public List<DynamicStagePattern> AvailablePatterns { get; } = new()
    {
        new() { Name = "3 Month Subscription", Description = "Monthly payments for 3 months", Frequency = StageFrequency.Monthly, StageCount = 3 },
        new() { Name = "6 Month Subscription", Description = "Monthly payments for 6 months", Frequency = StageFrequency.Monthly, StageCount = 6 },
        new() { Name = "Annual Subscription", Description = "Monthly payments for 12 months", Frequency = StageFrequency.Monthly, StageCount = 12 }
    };
    public ProjectType ProjectType { get; } = ProjectType.Invest; // Change to Fund or Subscribe to test pattern selection
}