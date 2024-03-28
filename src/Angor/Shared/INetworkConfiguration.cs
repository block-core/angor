using Angor.Shared.Models;
using Blockcore.Networks;

namespace Angor.Shared;

public interface INetworkConfiguration
{
    Network GetNetwork();
    void SetNetwork(Network network);
    SettingsUrl GetIndexerUrl();
    SettingsUrl GetExplorerUrl();
    List<SettingsUrl> GetDefaultIndexerUrls();
    List<SettingsUrl> GetDefaultRelayUrls();
    List<SettingsUrl> GetDefaultExplorerUrl();
}