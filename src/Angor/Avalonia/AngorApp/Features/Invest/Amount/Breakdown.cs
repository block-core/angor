namespace AngorApp.Features.Invest.Amount;

public record Breakdown(int Index, IAmountUI InvestAmount, double RatioOfTotal, DateTimeOffset ReleaseDate)
{
    public IAmountUI Amount => new AmountUI((long)(InvestAmount.Sats * RatioOfTotal));
}