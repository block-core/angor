using Blockcore.Networks;

namespace Angor.Shared;

public class NetworkMapper
{
    /// <summary>
    /// Map a blockcore network to an NBitcoin network type
    /// </summary>
    /// <param name="network"></param>
    /// <returns></returns>
    public static NBitcoin.Network Map(Network network)
    {
        return network.NetworkType switch
        {
            NetworkType.Mainnet => NBitcoin.Network.Main,
            NetworkType.Testnet => NBitcoin.Network.TestNet,
            NetworkType.Regtest => NBitcoin.Network.RegTest,
            _ => throw new InvalidOperationException()
        };
    }
}