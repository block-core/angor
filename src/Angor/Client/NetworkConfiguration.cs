using Angor.Shared;
using Angor.Shared.Models;
using Blockcore.Networks;

namespace Angor.Client;

public class NetworkConfiguration : INetworkConfiguration
{
    public static string AngorTestKey = "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";
    public static string AngorMainKey = "xpub661MyMwAqRbcGNxKe9aFkPisf3h32gHLJm8f9XAqx8FB1Nk6KngCY8hkhGqxFr2Gyb6yfUaQVbodxLoC1f3K5HU9LM1CXE59gkEXSGCCZ1B";

    public static long AngorCreateFeeSats = 10001; // version of script :)
    public static int AngorInvestFeePercentage = 1;
    public static short NostrEventIdKeyType = 1; //TODO David use an enum for this?

    public int GetAngorInvestFeePercentage => AngorInvestFeePercentage;

    public string GetAngorKey()
    {
        var network = GetNetwork();

        if (network.NetworkType == NetworkType.Mainnet)
        {
            return AngorMainKey;
        }

        return AngorTestKey;
    }

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
        if (currentNetwork.NetworkType == NetworkType.Mainnet)
        {
            return new SettingsUrl { Name = "", Url = "https://btc.indexer.angor.io/api" };
        }

        if (currentNetwork.NetworkType == NetworkType.Testnet)
        {
            if (currentNetwork.Name == "Angornet")
            {
                return new SettingsUrl { Name = "", Url = "https://tbtc.indexer.angor.io/api" };
            }

            if (currentNetwork.Name == "Testnet")
            {
                // todo find indexer url
            }

            if (currentNetwork.Name == "Testnet4")
            {
                // todo find indexer url
            }

            if (currentNetwork.Name == "Signet")
            {
                // todo find indexer url
            }
        }

        throw new ApplicationException("Network not set");
    }

    public SettingsUrl GetExplorerUrl()
    {
        if (currentNetwork.NetworkType == NetworkType.Mainnet)
        {
            return new SettingsUrl { Name = "", Url = "https://explorer.angor.io/btc/explorer" };
        }

        if (currentNetwork.NetworkType == NetworkType.Testnet)
        {
            if (currentNetwork.Name == "Angornet")
            {
                return new SettingsUrl { Name = "", Url = "https://explorer.angor.io/tbtc/explorer" };
            }

            if (currentNetwork.Name == "Testnet")
            {
                // todo find explorer url
            }

            if (currentNetwork.Name == "Testnet4")
            {
                // todo find explorer url
            }

            if (currentNetwork.Name == "Signet")
            {
                // todo find explorer url
            }
        }

        throw new ApplicationException("Network not set");
    }

    public List<SettingsUrl> GetDefaultIndexerUrls()
    {
        if (currentNetwork.NetworkType == NetworkType.Mainnet)
        {
            return new List<SettingsUrl>
            {
                new SettingsUrl { Name = "", Url = "https://btc.indexer.angor.io", IsPrimary = true },
            };
        }

        if (currentNetwork.NetworkType == NetworkType.Testnet)
        {
            if (currentNetwork.Name == "Angornet")
            {
                return new List<SettingsUrl>
                {
                    new SettingsUrl { Name = "", Url = "https://tbtc.indexer.angor.io", IsPrimary = true },
                };
            }

            if (currentNetwork.Name == "Testnet")
            {
                // todo find indexer url
            }

            if (currentNetwork.Name == "Testnet4")
            {
                // todo find indexer url
            }

            if (currentNetwork.Name == "Signet")
            {
                // todo find indexer url
            }
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
        if (currentNetwork.NetworkType == NetworkType.Mainnet)
        {
            return new List<SettingsUrl>
            {
                new SettingsUrl { Name = "", Url = "https://explorer.angor.io/btc/explorer", IsPrimary = true },
            };
        }

        if (currentNetwork.NetworkType == NetworkType.Testnet)
        {
            if (currentNetwork.Name == "Angornet")
            {
                return new List<SettingsUrl>
                {
                    new SettingsUrl { Name = "", Url = "https://explorer.angor.io/tbtc/explorer", IsPrimary = true },
                };
            }

            if (currentNetwork.Name == "Testnet")
            {
                // todo find explorer url
            }

            if (currentNetwork.Name == "Testnet4")
            {
                // todo find explorer url
            }

            if (currentNetwork.Name == "Signet")
            {
                // todo find explorer url
            }
        }

        throw new ApplicationException("Network not set");
    }

}