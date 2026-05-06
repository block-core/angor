using NBitcoin;

namespace Angor.Primitives.Network;

public class ConsensusFactory
{
    public NBitcoin.Network? NBitcoinNetwork { get; set; }

    public Transaction CreateTransaction()
    {
        return NBitcoinNetwork?.CreateTransaction() ?? Transaction.Create(NBitcoin.Network.Main);
    }

    public Transaction CreateTransaction(string hex)
    {
        return Transaction.Parse(hex, NBitcoinNetwork ?? NBitcoin.Network.Main);
    }
}
