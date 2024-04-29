using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Blockcore.Networks;

namespace Angor.Client;

public class NetworkConfiguration : INetworkConfiguration
{
    public static string AngorTestKey = "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";
    public static string AngorMainKey = "todo: add an Angor key here";

    public static long AngorCreateFeeSats = 10000;


    private Network currentNetwork;

    public void SetNetwork(Network network)
    {
        currentNetwork = network;
    }

    public Network GetNetwork()
    {
        if (currentNetwork == null)
        {
            throw new ApplicationException("Network not set");
        }

        return currentNetwork;
    }

    public SettingsUrl GetIndexerUrl()
    {
        if (currentNetwork.NetworkType == NetworkType.Testnet)
        {
            return new SettingsUrl { Name = "", Url = "https://tbtc.indexer.angor.io/api" };
        }

        if (currentNetwork.NetworkType == NetworkType.Mainnet)
        {
            return new SettingsUrl { Name = "", Url = "https://btc.indexer.angor.io/api" };
        }

        throw new ApplicationException("Network not set");
    }

    public SettingsUrl GetExplorerUrl()
    {
        if (currentNetwork.NetworkType == NetworkType.Testnet)
        {
            return new SettingsUrl { Name = "", Url = "https://explorer.angor.io/tbtc/explorer" };
        }

        if (currentNetwork.NetworkType == NetworkType.Mainnet)
        {
            return new SettingsUrl { Name = "", Url = "https://explorer.angor.io/btc/explorer" };
        }

        throw new ApplicationException("Network not set");
    }

    public List<SettingsUrl> GetDefaultIndexerUrls()
    {
        if (currentNetwork.NetworkType == NetworkType.Testnet)
        {
            return new List<SettingsUrl>
            {
                new SettingsUrl { Name = "", Url = "https://tbtc.indexer.angor.io", IsPrimary = true },
            };
        }

        if (currentNetwork.NetworkType == NetworkType.Mainnet)
        {
            return new List<SettingsUrl>
            {
                new SettingsUrl { Name = "", Url = "https://btc.indexer.angor.io", IsPrimary = true },
            };
        }

        throw new ApplicationException("Network not set");

    }

    public List<SettingsUrl> GetDefaultRelayUrls()
    {
        return new List<SettingsUrl>
        {
            new SettingsUrl { Name = "", Url = "wss://relay.angor.io", IsPrimary = true },
            new SettingsUrl { Name = "", Url = "wss://relay2.angor.io", IsPrimary = true },

        };
    }

    public List<SettingsUrl> GetDefaultExplorerUrl()
    {
        if (currentNetwork.NetworkType == NetworkType.Testnet)
        {
            return new List<SettingsUrl>
            {
                new SettingsUrl { Name = "", Url = "https://explorer.angor.io/tbtc/explorer", IsPrimary = true },
            };
        }

        if (currentNetwork.NetworkType == NetworkType.Mainnet)
        {
            return new List<SettingsUrl>
            {
                new SettingsUrl { Name = "", Url = "https://explorer.angor.io/btc/explorer", IsPrimary = true },
            };
        }

        throw new ApplicationException("Network not set");
    }

}