using Angor.Sdk.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace AngorApp.Model.Amounts;

public class AmountUI(long sats, string symbol = "BTC") : ValueObject, IAmountUI
{
    public long Sats { get; } = sats;
    public string Symbol { get; } = symbol;
    
    public static AmountUI FromBtc(int btc) => new(btc * 100_000_000);
    public static AmountUI FromBtc(decimal btc) => new((long)(btc * 100_000_000)); 
    public static AmountUI FromBtc(double btc) => new((long)(btc * 100_000_000));
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Sats;
    }
}