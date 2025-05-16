namespace AngorApp.Features.Invest.Amount;

public interface IAmountViewModel
{
    public long? Amount { get; set; }
    IProject Project { get; }
    IEnumerable<Breakdown> StageBreakdowns { get; }
}