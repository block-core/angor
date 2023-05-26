using Angor.Client.Shared.Types;
using Blockcore.Networks;

namespace Angor.Client;

public interface INetworkConfiguration
{
    Network GetNetwork();
    IndexerUrl getIndexerUrl();
}