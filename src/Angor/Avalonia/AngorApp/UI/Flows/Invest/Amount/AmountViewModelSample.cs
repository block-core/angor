namespace AngorApp.UI.Flows.Invest.Amount;

public class AmountViewModelSample : IAmountViewModel
{
    public long? Amount { get; set; } = 20000;

    public IEnumerable<Breakdown> StageBreakdowns { get; } = new List<Breakdown>
    {
        new(1, new AmountUI(120), 0.2m, DateTime.Now),
        new(2, new AmountUI(120), 0.4m, DateTime.Now.AddMonths(1)),
        new(3, new AmountUI(120), 0.6m, DateTime.Now.AddMonths(2).AddDays(5)),
    };

    public IObservable<bool> IsValid { get; } = Observable.Return(true);
    public bool RequiresFounderApproval { get; } = true; // Show penalty warning in design mode (above threshold scenario)
}