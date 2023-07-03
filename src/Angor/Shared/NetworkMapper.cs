using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.Networks;

namespace Angor.Shared;

public class NetworkMapper
{
    /// <summary>
    /// Map a blockcore network to an NBitcoin network type
    /// </summary>
    /// <param name="network"></param>
    /// <returns></returns>
    public static NBitcoin.Network Map(Blockcore.Networks.Network network)
    {
        if (network.NetworkType == NetworkType.Mainnet)
            return NBitcoin.Network.Main;

        if (network.NetworkType == NetworkType.Testnet)
            return NBitcoin.Network.TestNet;

        if (network.NetworkType == NetworkType.Regtest)
            return NBitcoin.Network.RegTest;

        throw new InvalidOperationException();
    }
}