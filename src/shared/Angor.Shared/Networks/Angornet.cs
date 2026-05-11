using Angor.Primitives.Network;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Angor.Shared.Networks;

public class Angornet : BitcoinMain
{
    public Angornet()
    {
        Name = "Angornet";
        AdditionalNames = new List<string> { "angornet" };
        NetworkType = NetworkType.Testnet;
        CoinTicker = "TBTC";
        NBitcoinNetwork = Network.TestNet;
        Consensus = new Angor.Primitives.Network.Consensus { CoinType = 1 };

        var encoder = new Bech32Encoder(System.Text.Encoding.ASCII.GetBytes("tb"));
        Bech32Encoders = new Bech32Encoder[2];
        Bech32Encoders[0] = encoder;
        Bech32Encoders[1] = encoder;
    }
}
