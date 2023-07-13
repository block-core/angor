using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Blockcore.Networks;

namespace Angor.Client;

public class NetworkConfiguration : INetworkConfiguration
{
    public Network GetNetwork()
    {
        return new BitcoinSignet();
    }

    public IndexerUrl GetIndexerUrl()
    {
        return new IndexerUrl{Symbol = "", Url = "http://10.22.156.163:9910/api"};
    }

}