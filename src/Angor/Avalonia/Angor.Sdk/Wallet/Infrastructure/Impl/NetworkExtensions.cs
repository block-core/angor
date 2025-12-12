using Angor.Sdk.Wallet.Domain;
using NBitcoin;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public static class NetworkExtensions
{
    public static Network ToNBitcoin(this BitcoinNetwork bitcoinNetwork)
    {
        return bitcoinNetwork switch
        {
            BitcoinNetwork.Mainnet => Network.Main,
            BitcoinNetwork.Testnet => Network.TestNet,
            _ => throw new ArgumentOutOfRangeException(nameof(bitcoinNetwork), bitcoinNetwork, null)
        };
    }

    public static BitcoinNetwork FromNBitcoin(this Network bitcoinNetwork)
    {
        if (bitcoinNetwork == Network.Main)
        {
            return BitcoinNetwork.Mainnet;
        }

        if (bitcoinNetwork == Network.Main)
        {
            return BitcoinNetwork.Mainnet;
        }


        if (bitcoinNetwork == Network.TestNet)
        {
            return BitcoinNetwork.Testnet;
        }

        throw new InvalidCastException("Unsupported network");
    }
}