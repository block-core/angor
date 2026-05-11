namespace Angor.Primitives.Network;

public class Consensus
{
    public uint CoinType { get; set; }
    public ConsensusFactory ConsensusFactory { get; set; } = new();
}
