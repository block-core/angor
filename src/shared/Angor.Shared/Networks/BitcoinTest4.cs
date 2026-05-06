using Angor.Primitives.Network;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Angor.Shared.Networks;

public class BitcoinTest4 : BitcoinMain
{
    public BitcoinTest4()
    {
        Name = "TestNet4";
        AdditionalNames = new List<string> { "testnet4" };
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
