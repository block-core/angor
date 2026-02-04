namespace AngorApp.UI.Flows.Invest.Amount;

public record Breakdown(IAmountUI InvestAmount, decimal RatioOfTotal, DateTimeOffset ReleaseDate)
{
    public IAmountUI Amount => new AmountUI((long)(InvestAmount.Sats * RatioOfTotal));
}