using Angor.Primitives.Network;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Angor.Shared.Networks;

public class BitcoinSignet : AngorNetwork
{
    public BitcoinSignet()
    {
        Name = "Signet";
        AdditionalNames = new List<string> { "bitcoin-signet", "btc-signet" };
        NetworkType = NetworkType.Testnet;
        NBitcoinNetwork = Network.TestNet;
        Consensus = new Consensus { CoinType = 1 };

        var encoder = new Bech32Encoder("tb");
        Bech32Encoders = new Bech32Encoder[2];
        Bech32Encoders[0] = encoder;
        Bech32Encoders[1] = encoder;
    }
}
