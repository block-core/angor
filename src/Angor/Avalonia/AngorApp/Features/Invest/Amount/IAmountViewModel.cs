namespace AngorApp.Features.Invest.Amount;

public interface IAmountViewModel
{
    public long? Amount { get; set; }
    IEnumerable<Breakdown> StageBreakdowns { get; }
}