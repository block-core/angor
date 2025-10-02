using Angor.Contexts.Wallet.Domain;

namespace Angor.UI.Model;

public class AmountUI(long sats, string symbol = "BTC") : IAmountUI
{
    public long Sats { get; } = sats;
    public string Symbol { get; } = symbol;
}