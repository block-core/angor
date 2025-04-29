using Angor.Contexts.Wallet.Domain;

namespace Angor.UI.Model;

public class AmountUI(long sats) : IAmountUI
{
    public long Sats { get; } = sats;
}