using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Blockcore.Networks;

namespace Angor.Contests.CrossCutting;

public class NetworkConfiguration : INetworkConfiguration
{
    public static string AngorTestKey = "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";
    
    private Angornet network;

    public Network GetNetwork()
    {
        return new Angornet();
    }

    public void SetNetwork(Network network)
    {
        throw new NotSupportedException("Cannot switch networks");
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

    public List<SettingsUrl> GetDiscoveryRelays()
    {
        throw new NotImplementedException();
    }

    public string GetAngorKey()
    {
        return AngorTestKey;
    }

    public Dictionary<string, bool> GetDefaultFeatureFlags(string network)
    {
        throw new NotImplementedException();
    }

    public int GetAngorInvestFeePercentage { get; }
}