using Angor.Primitives.Network;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Angor.Shared.Networks;

public class LiquidMain : BitcoinMain
{
    public LiquidMain()
    {
        Name = "Liquid";
        AdditionalNames = new List<string> { "liquid", "liquid-mainnet", "liquid-main" };
        NetworkType = NetworkType.Mainnet;
        CoinTicker = "LBTC";
        NBitcoinNetwork = NBitcoin.Altcoins.Liquid.Instance.Mainnet;
        Consensus = new Angor.Primitives.Network.Consensus { CoinType = 1776 };

        var encoder = new Bech32Encoder(System.Text.Encoding.ASCII.GetBytes("ex"));
        Bech32Encoders = new Bech32Encoder[2];
        Bech32Encoders[0] = encoder;
        Bech32Encoders[1] = encoder;
    }
}
