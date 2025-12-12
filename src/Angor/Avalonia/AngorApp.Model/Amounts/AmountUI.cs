using Angor.Sdk.Wallet.Domain;

namespace AngorApp.Model.Amounts;

public class AmountUI(long sats, string symbol = "BTC") : IAmountUI
{
    public long Sats { get; } = sats;
    public string Symbol { get; } = symbol;
}