namespace AngorApp.Flows.Invest.Amount;

public interface IAmountViewModel
{
    public long? Amount { get; set; }
    IEnumerable<Breakdown> StageBreakdowns { get; }
    IObservable<bool> IsValid { get; }
}