using Angor.Shared.Models;
using Blockcore.Networks;

namespace Angor.Shared;

public interface INetworkConfiguration
{
    Network GetNetwork();
    bool GetDebugMode();
    void SetNetwork(Network network);
    String GetGenesisBlockHash();
    string GetNetworkNameFromGenesisBlockHash(string genesisBlockHash);

    List<SettingsUrl> GetDefaultIndexerUrls();
    List<SettingsUrl> GetDefaultRelayUrls();
    List<SettingsUrl> GetDefaultExplorerUrls();
    List<SettingsUrl> GetDefaultChatAppUrls();
    List<SettingsUrl> GetDiscoveryRelays();
    int GetAngorInvestFeePercentage { get; }
    string GetAngorKey();
    Dictionary<string, bool> GetDefaultFeatureFlags(string network);
}