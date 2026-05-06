using Angor.Primitives.Network;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Angor.Shared.Networks;

public class BitcoinMain : AngorNetwork
{
    public BitcoinMain()
    {
        Name = "Main";
        AdditionalNames = new List<string> { "Mainnet" };
        NetworkType = NetworkType.Mainnet;
        NBitcoinNetwork = Network.Main;
        Consensus = new Consensus { CoinType = 0 };

        var encoder = new Bech32Encoder("bc");
        Bech32Encoders = new Bech32Encoder[2];
        Bech32Encoders[0] = encoder; // WITNESS_PUBKEY_ADDRESS
        Bech32Encoders[1] = encoder; // WITNESS_SCRIPT_ADDRESS
    }
}
