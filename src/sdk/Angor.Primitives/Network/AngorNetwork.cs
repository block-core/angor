using NBitcoin;
using NBitcoin.DataEncoders;

namespace Angor.Primitives.Network;

/// <summary>
/// Lightweight network definition replacing Blockcore.Networks.Network.
/// Wraps an NBitcoin.Network for transaction operations while providing
/// custom properties for Angor's network identification needs.
/// </summary>
public class AngorNetwork
{
    public string Name { get; set; } = "";
    public List<string> AdditionalNames { get; set; } = new();
    public NetworkType NetworkType { get; set; }
    public string CoinTicker { get; set; } = "";
    public Consensus Consensus { get; set; } = new();
    public Bech32Encoder?[] Bech32Encoders { get; set; } = new Bech32Encoder[2];

    /// <summary>The underlying NBitcoin network used for transaction serialization.</summary>
    public NBitcoin.Network NBitcoinNetwork { get; set; } = NBitcoin.Network.Main;

    public Transaction CreateTransaction()
    {
        return NBitcoinNetwork.CreateTransaction();
    }

    public Transaction CreateTransaction(string hex)
    {
        return Transaction.Parse(hex, NBitcoinNetwork);
    }

    public static implicit operator NBitcoin.Network(AngorNetwork angorNetwork) => angorNetwork.NBitcoinNetwork;
}
