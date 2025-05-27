using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Blockcore.Networks;

namespace Angor.Contests.CrossCutting;

public class NetworkConfiguration2 : INetworkConfiguration
{
    private Angornet network;

    public Network GetNetwork()
    {
        return new Angornet();
    }

    public void SetNetwork(Network network)
    {
        this.network = new Angornet();
    }

    public string GetGenesisBlockHash()
    {
        throw new NotImplementedException();
    }

    public string GetNetworkNameFromGenesisBlockHash(string genesisBlockHash)
    {
        throw new NotImplementedException();
    }

    public List<SettingsUrl> GetDefaultIndexerUrls()
    {
        return new List<SettingsUrl>
        {
            new SettingsUrl { Name = "", Url = "https://test.indexer.angor.io", IsPrimary = true },
        };
    }

    public List<SettingsUrl> GetDefaultRelayUrls()
    {
        return new List<SettingsUrl>
        {
            new SettingsUrl { Name = "", Url = "wss://relay.angor.io", IsPrimary = true },
        };
    }

    public List<SettingsUrl> GetDefaultExplorerUrls()
    {
        throw new NotImplementedException();
    }

    public List<SettingsUrl> GetDefaultChatAppUrls()
    {
        throw new NotImplementedException();
    }

    public string GetAngorKey()
    {
        throw new NotImplementedException();
    }

    public Dictionary<string, bool> GetDefaultFeatureFlags(string network)
    {
        throw new NotImplementedException();
    }

    public int GetAngorInvestFeePercentage { get; }
}