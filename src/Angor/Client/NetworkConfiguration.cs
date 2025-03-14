using Angor.Shared;
using Angor.Shared.Models;
using Blockcore.Networks;

namespace Angor.Client;

public class NetworkConfiguration : INetworkConfiguration
{
    public static string AngorTestKey = "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";
    public static string AngorMainKey = "xpub661MyMwAqRbcGNxKe9aFkPisf3h32gHLJm8f9XAqx8FB1Nk6KngCY8hkhGqxFr2Gyb6yfUaQVbodxLoC1f3K5HU9LM1CXE59gkEXSGCCZ1B";

    public static long AngorCreateFeeSats = 10001; // versioning :)
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
                    new SettingsUrl { Name = "", Url = "https://tbtc.indexer.angor.io", IsPrimary = false },
                    new SettingsUrl { Name = "", Url = "https://mempool.thedude.pro", IsPrimary = true },
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
    
    public string GetGenesisBlockHash()
    {
        // Determine the correct genesis block hash based on the network type
        return currentNetwork.Name switch
        {
            "Mainnet" => "000000000019d6689c085ae165831e93",
            "Testnet" => "000000000933ea01ad0ee984209779ba",
            "Signet" => "00000000020f01e33f91b6c7c5d3a3f8",
            "Regtest" => "0f9195cbdb894feda6ee07798e0d597d",
            "Angornet" => "00000008819873e925422c1ff0f99f7cc9bbb232af63a077a480a3633bee1ef6",
            _ => throw new NotSupportedException($"Network type {currentNetwork.NetworkType.ToString()} is not supported")
        };
    }
    
    public string GetNetworkNameFromGenesisBlockHash(string genesisBlockHash)
    {
        return genesisBlockHash switch
        {
            "000000000019d6689c085ae165831e93" => "Mainnet",
            "000000000933ea01ad0ee984209779ba" => "Testnet",
            "00000000020f01e33f91b6c7c5d3a3f8" => "Signet",
            "0f9195cbdb894feda6ee07798e0d597d" => "Regtest",
            "00000008819873e925422c1ff0f99f7cc9bbb232af63a077a480a3633bee1ef6" => "Angornet",
            _ => "Unknown"
        };
    }



}