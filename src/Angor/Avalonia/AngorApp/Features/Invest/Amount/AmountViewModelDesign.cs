namespace AngorApp.Features.Invest.Amount;

public class AmountViewModelDesign : IAmountViewModel
{
    public long? Amount { get; set; } = 20000;

    public IEnumerable<Breakdown> StageBreakdowns { get; } = new List<Breakdown>
    {
        new(1, new AmountUI(120), 0.2, DateTime.Now),
        new(2, new AmountUI(120), 0.4, DateTime.Now.AddMonths(1)),
        new(3, new AmountUI(120), 0.6, DateTime.Now.AddMonths(2).AddDays(5)),
    };
}