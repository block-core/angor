namespace AngorApp.Design;

public class DesignTimeAmount : IAmountUI
{
    public long Sats { get; set; }
    public string Symbol { get; } = "TBTC"; // Design time symbol, can be anything
}