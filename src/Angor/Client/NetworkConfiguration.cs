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
    public static NostrKeyType NostrEventIdKeyType = NostrKeyType.EventId;
    
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
    private bool? _debugMode;

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

    public bool GetDebugMode()
    {
        return _debugMode ?? false;
    }

    public void SetDebugMode(bool? debugMode=false)
    {
        _debugMode = debugMode;
    }

    public List<SettingsUrl> GetDefaultIndexerUrls()
    {
        if (currentNetwork.NetworkType == NetworkType.Mainnet)
        {
            if (currentNetwork.Name == "Liquid")
            {
                return new List<SettingsUrl>
                {
                    new SettingsUrl { Name = "", Url = "https://liquid.angor.online", IsPrimary = true },
                };
            }

            return new List<SettingsUrl>
            {
                new SettingsUrl { Name = "", Url = "https://indexer.angor.io", IsPrimary = false },
                new SettingsUrl { Name = "", Url = "https://fulcrum.angor.online", IsPrimary = true },
                new SettingsUrl { Name = "", Url = "https://electrs.angor.online", IsPrimary = false },
                new SettingsUrl { Name = "", Url = "https://cyphermunkhouse.angor.online", IsPrimary = false },
                new SettingsUrl { Name = "", Url = "https://indexer.angor.fund", IsPrimary = false },
            };
        }
        
        if (currentNetwork.NetworkType == NetworkType.Testnet)
        {
            if (currentNetwork.Name == "Angornet")
            {
                return new List<SettingsUrl>
                {
                    new SettingsUrl { Name = "", Url = "https://test.indexer.angor.io", IsPrimary = false },
                    new SettingsUrl { Name = "", Url = "https://signet.angor.online", IsPrimary = true},
                    new SettingsUrl { Name = "", Url = "https://signet2.angor.online", IsPrimary = false},
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
        if (currentNetwork.NetworkType == NetworkType.Mainnet)
        {
            return new List<SettingsUrl>
            {
                new SettingsUrl { Name = "", Url = "wss://relay.angor.io", IsPrimary = true },
                new SettingsUrl { Name = "", Url = "wss://relay2.angor.io", IsPrimary = true },
                new SettingsUrl { Name = "", Url = "wss://relay.damus.io", IsPrimary = true },
                new SettingsUrl { Name = "", Url = "wss://nos.lol", IsPrimary = true },
                new SettingsUrl { Name = "", Url = "wss://nostr-01.yakihonne.com", IsPrimary = true },
                new SettingsUrl { Name = "", Url = "wss://nostr-02.yakihonne.com", IsPrimary = true },
            };
        }

        return new List<SettingsUrl>
        {
            new SettingsUrl { Name = "", Url = "wss://relay.angor.io", IsPrimary = true },
            new SettingsUrl { Name = "", Url = "wss://relay2.angor.io", IsPrimary = true },

        };
    }

    public List<SettingsUrl> GetDefaultExplorerUrls()
    {
        if (currentNetwork.NetworkType == NetworkType.Mainnet)
        {
            if (currentNetwork.Name == "Liquid")
            {
                return new List<SettingsUrl>
                {
                    new SettingsUrl { Name = "", Url = "https://liquid.angor.online", IsPrimary = true },
                };
            }

            return new List<SettingsUrl>
            {
                new SettingsUrl { Name = "", Url = "https://explorer.angor.io", IsPrimary = false },
                new SettingsUrl { Name = "", Url = "https://fulcrum.angor.online", IsPrimary = true},
                new SettingsUrl { Name = "", Url = "https://electrs.angor.online", IsPrimary = false },
                new SettingsUrl { Name = "", Url = "https://cyphermunkhouse.angor.online", IsPrimary = false },
                new SettingsUrl { Name = "", Url = "https://indexer.angor.fund", IsPrimary = false },
            };
        }

        if (currentNetwork.NetworkType == NetworkType.Testnet)
        {
            if (currentNetwork.Name == "Angornet")
            {
                return new List<SettingsUrl>
                {
                    new SettingsUrl { Name = "", Url = "https://test.explorer.angor.io", IsPrimary = false },
                    new SettingsUrl { Name = "", Url = "https://signet.angor.online", IsPrimary = true },
                    new SettingsUrl { Name = "", Url = "https://signet2.angor.online", IsPrimary = false},
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

    public List<SettingsUrl> GetDiscoveryRelays()
    {
        return new List<SettingsUrl>
        {
            new SettingsUrl { Name = "wss://purplerelay.com", Url = "wss://purplerelay.com" },
            new SettingsUrl { Name = "wss://discovery.eu.nostria.app", Url = "wss://discovery.eu.nostria.app" },
        };
    }

    public string GetGenesisBlockHash()
    {
        // Determine the correct genesis block hash based on the network type
        return currentNetwork.Name switch
        {
            "Main" => "000000000019d6689c085ae165831e93",
            "Testnet" => "000000000933ea01ad0ee984209779ba",
            "Signet" => "00000000020f01e33f91b6c7c5d3a3f8",
            "Regtest" => "0f9195cbdb894feda6ee07798e0d597d",
            "Angornet" => "00000008819873e925422c1ff0f99f7cc9bbb232af63a077a480a3633bee1ef6",
            "Liquid" => "d767f204777d8ebd0825f4f26c3d773c0d3f40268dc6afb3632a0fcbd49fde45",
            _ => throw new NotSupportedException($"Network type {currentNetwork.NetworkType.ToString()} is not supported")
        };
    }

    public string GetNetworkNameFromGenesisBlockHash(string genesisBlockHash)
    {
        return genesisBlockHash switch
        {
            "000000000019d6689c085ae165831e93" => "Main",
            "000000000933ea01ad0ee984209779ba" => "Testnet",
            "00000000020f01e33f91b6c7c5d3a3f8" => "Signet",
            "0f9195cbdb894feda6ee07798e0d597d" => "Regtest",
            "00000008819873e925422c1ff0f99f7cc9bbb232af63a077a480a3633bee1ef6" => "Angornet",
            "d767f204777d8ebd0825f4f26c3d773c0d3f40268dc6afb3632a0fcbd49fde45" => "Liquid",
            _ => "Unknown"
        };
    }

    public List<SettingsUrl> GetDefaultChatAppUrls()
    {
        return new List<SettingsUrl>
            {
                new SettingsUrl { Name = "Angor Chat", Url = "https://chat.angor.io/dm", IsPrimary = true },
                new SettingsUrl { Name = "Primal", Url = "https://primal.net/dms", IsPrimary = false },
            };
    }
    public Dictionary<string, bool> GetDefaultFeatureFlags(string network)
    {
        return network switch
        {
            "Main" => new() {},
            "Testnet" => new() { },
            "Signet" => new() { },
            "Regtest" => new() { },
            "Angornet" => new()
            {
                {"HW_Support", false}
            },
            "Liquid" => new() {},
            _ => throw new NotSupportedException($"Network type {currentNetwork.NetworkType.ToString()} is not supported")
        };
    }
}