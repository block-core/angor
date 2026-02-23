namespace AngorApp.UI.Flows.InvestV2.Model;

public record Breakdown(IAmountUI InvestAmount, decimal RatioOfTotal, DateTimeOffset ReleaseDate)
{
    public IAmountUI Amount => new AmountUI((long)(InvestAmount.Sats * RatioOfTotal));
}