using Angor.Shared.Models;
using Blockcore.Networks;

namespace Angor.Shared;

public interface INetworkConfiguration
{
    Network GetNetwork();
    void SetNetwork(Network network);
    String GetGenesisBlockHash();
    SettingsUrl GetIndexerUrl();
    SettingsUrl GetExplorerUrl();
    List<SettingsUrl> GetDefaultIndexerUrls();
    List<SettingsUrl> GetDefaultRelayUrls();
    List<SettingsUrl> GetDefaultExplorerUrl();

    int GetAngorInvestFeePercentage { get; }
    string GetAngorKey();
}