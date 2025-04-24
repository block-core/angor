namespace AngorApp.Features.Invest.Amount;

public record Breakdown(
    int Index,
    long Amount,
    double Weight,
    DateTime ReleaseDate)
{
    public long InvestmentSats => (long)(Amount * Weight);

    public string Description =>
        $"Stage {Index}: invest {InvestmentSats} sats that will be released on {ReleaseDate:d}";
}