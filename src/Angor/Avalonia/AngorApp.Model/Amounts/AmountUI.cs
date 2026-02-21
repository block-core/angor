using Angor.Sdk.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace AngorApp.Model.Amounts;

public class AmountUI(long sats, string? symbol = null) : ValueObject, IAmountUI
{
    /// <summary>
    /// The default currency symbol used when none is explicitly provided.
    /// Set at startup from the network configuration (e.g. "BTC" for mainnet, "TBTC" for testnet).
    /// </summary>
    public static string DefaultSymbol { get; set; } = "BTC";

    public long Sats { get; } = sats;
    public string Symbol { get; } = symbol ?? DefaultSymbol;
    
    public static AmountUI FromBtc(int btc) => new(btc * 100_000_000);
    public static AmountUI FromBtc(decimal btc) => new((long)(btc * 100_000_000)); 
    public static AmountUI FromBtc(double btc) => new((long)(btc * 100_000_000));
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Sats;
    }
}