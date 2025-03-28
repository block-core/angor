using Angor.Shared.Models;
using Blockcore.Networks;

namespace Angor.Shared;

public interface INetworkConfiguration
{
    Network GetNetwork();
    void SetNetwork(Network network);
    String GetGenesisBlockHash();
    string GetNetworkNameFromGenesisBlockHash(string genesisBlockHash);

    List<SettingsUrl> GetDefaultIndexerUrls();
    List<SettingsUrl> GetDefaultRelayUrls();
    List<SettingsUrl> GetDefaultExplorerUrls();

    int GetAngorInvestFeePercentage { get; }
    string GetAngorKey();
}