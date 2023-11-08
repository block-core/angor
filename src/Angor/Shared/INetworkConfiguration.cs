using Angor.Shared.Models;
using Blockcore.Networks;

namespace Angor.Shared;

public interface INetworkConfiguration
{
    Network GetNetwork();
    SettingsUrl GetIndexerUrl();
    SettingsUrl GetExplorerUrl();
}