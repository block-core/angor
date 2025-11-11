namespace AngorApp.UI.Flows.Invest.Amount;

public interface IAmountViewModel
{
    public long? Amount { get; set; }
    IEnumerable<Breakdown> StageBreakdowns { get; }
    IObservable<bool> IsValid { get; }
    bool RequiresFounderApproval { get; }
}