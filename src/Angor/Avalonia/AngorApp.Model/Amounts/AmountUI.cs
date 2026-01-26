using Angor.Sdk.Wallet.Domain;

namespace AngorApp.Model.Amounts;

public class AmountUI(long sats, string symbol = "BTC") : IAmountUI
{
    public long Sats { get; } = sats;
    public string Symbol { get; } = symbol;
    
    public static AmountUI FromBtc(int btc) => new(btc * 100_000_000);
    public static AmountUI FromBtc(decimal btc) => new((long)(btc * 100_000_000)); 
    public static AmountUI FromBtc(double btc) => new((long)(btc * 100_000_000));
}