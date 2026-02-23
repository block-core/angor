using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Blockcore.Networks;

namespace Angor.Sdk.Common;

public class NetworkConfiguration : INetworkConfiguration
{
    public const string AngorTestKey = "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";
    public const string AngorMainKey = "xpub661MyMwAqRbcGNxKe9aFkPisf3h32gHLJm8f9XAqx8FB1Nk6KngCY8hkhGqxFr2Gyb6yfUaQVbodxLoC1f3K5HU9LM1CXE59gkEXSGCCZ1B";

    public const long AngorCreateFeeSats = 10001;
    public const int AngorInvestFeePercentage = 1;
    public const short NostrEventIdKeyType = 1;

    Network currentNetwork = null!;
    bool? debugMode;

    public void SetNetwork(Network network) => currentNetwork = network;

    public Network GetNetwork() => currentNetwork ?? throw new ApplicationException("Network not set");

    public bool GetDebugMode() => debugMode ?? false;

    public void SetDebugMode(bool? debugMode = false) => this.debugMode = debugMode;

    public int GetAngorInvestFeePercentage => AngorInvestFeePercentage;

    public string GetAngorKey()
    {
        var network = GetNetwork();
        return network.NetworkType == NetworkType.Mainnet ? AngorMainKey : AngorTestKey;
    }

    public List<SettingsUrl> GetDefaultIndexerUrls()
    {
        var network = GetNetwork();
        if (network.NetworkType == NetworkType.Mainnet)
        {
            if (network.Name == "Liquid")
            {
                return new List<SettingsUrl>
                {
                    new() { Name = string.Empty, Url = "https://liquid.angor.online", IsPrimary = true },
                };
            }

            return new List<SettingsUrl>
            {
                new() { Name = string.Empty, Url = "https://indexer.angor.io", IsPrimary = false },
                new() { Name = string.Empty, Url = "https://fulcrum.angor.online", IsPrimary = true },
                new() { Name = string.Empty, Url = "https://electrs.angor.online", IsPrimary = false },
                new() { Name = string.Empty, Url = "https://cyphermunkhouse.angor.online", IsPrimary = false },
                new() { Name = string.Empty, Url = "https://indexer.angor.fund", IsPrimary = false },
            };
        }

        if (network.NetworkType == NetworkType.Testnet && network.Name == "Angornet")
        {
            return new List<SettingsUrl>
            {
                new() { Name = string.Empty, Url = "https://test.indexer.angor.io", IsPrimary = false },
                new() { Name = string.Empty, Url = "https://signet.angor.online", IsPrimary = true },
                new() { Name = string.Empty, Url = "https://signet2.angor.online", IsPrimary = false },
            };
        }

        throw new ApplicationException("Network not set");
    }

    public List<SettingsUrl> GetDefaultRelayUrls()
    {
        var network = GetNetwork();
        if (network.NetworkType == NetworkType.Mainnet)
        {
            return new List<SettingsUrl>
            {
                new() { Name = string.Empty, Url = "wss://relay.angor.io", IsPrimary = true },
                new() { Name = string.Empty, Url = "wss://relay2.angor.io", IsPrimary = true },
                new() { Name = string.Empty, Url = "wss://relay.damus.io", IsPrimary = true },
                new() { Name = string.Empty, Url = "wss://nos.lol", IsPrimary = true },
                new() { Name = string.Empty, Url = "wss://nostr-01.yakihonne.com", IsPrimary = true },
                new() { Name = string.Empty, Url = "wss://nostr-02.yakihonne.com", IsPrimary = true },
            };
        }

        return new List<SettingsUrl>
        {
            new() { Name = string.Empty, Url = "wss://relay.angor.io", IsPrimary = true },
            new() { Name = string.Empty, Url = "wss://relay2.angor.io", IsPrimary = true },
        };
    }

    public List<SettingsUrl> GetDefaultExplorerUrls()
    {
        var network = GetNetwork();
        if (network.NetworkType == NetworkType.Mainnet)
        {
            if (network.Name == "Liquid")
            {
                return new List<SettingsUrl>
                {
                    new() { Name = string.Empty, Url = "https://liquid.angor.online", IsPrimary = true },
                };
            }

            return new List<SettingsUrl>
            {
                new() { Name = string.Empty, Url = "https://explorer.angor.io", IsPrimary = false },
                new() { Name = string.Empty, Url = "https://fulcrum.angor.online", IsPrimary = true },
                new() { Name = string.Empty, Url = "https://electrs.angor.online", IsPrimary = false },
                new() { Name = string.Empty, Url = "https://cyphermunkhouse.angor.online", IsPrimary = false },
                new() { Name = string.Empty, Url = "https://indexer.angor.fund", IsPrimary = false },
            };
        }

        if (network.NetworkType == NetworkType.Testnet && network.Name == "Angornet")
        {
            return new List<SettingsUrl>
            {
                new() { Name = string.Empty, Url = "https://test.explorer.angor.io", IsPrimary = false },
                new() { Name = string.Empty, Url = "https://signet.angor.online", IsPrimary = true },
                new() { Name = string.Empty, Url = "https://signet2.angor.online", IsPrimary = false },
            };
        }

        throw new ApplicationException("Network not set");
    }

    public List<SettingsUrl> GetDefaultChatAppUrls() =>
        new()
        {
            new() { Name = "Angor Chat", Url = "https://chat.angor.io/dm", IsPrimary = true },
            new() { Name = "Primal", Url = "https://primal.net/dms", IsPrimary = false },
        };

    public List<SettingsUrl> GetDefaultImageServerUrls() =>
        new()
        {
            // Blossom-compatible servers (NIP-B7 / BUD-02)
            new() { Name = "nostr.build", Url = "https://nostr.build", IsPrimary = true },
            new() { Name = "blossom.primal.net", Url = "https://blossom.primal.net", IsPrimary = false },
            new() { Name = "nostria (Blossom)", Url = "https://mibo.eu.nostria.app", IsPrimary = false },
        };

    public List<SettingsUrl> GetDiscoveryRelays() =>
        new()
        {
            new SettingsUrl { Name = "wss://purplerelay.com", Url = "wss://purplerelay.com" },
            new SettingsUrl { Name = "wss://discovery.eu.nostria.app", Url = "wss://discovery.eu.nostria.app" },
        };

    public string GetGenesisBlockHash()
    {
        var network = GetNetwork();
        return network.Name switch
        {
            "Main" => "000000000019d6689c085ae165831e93",
            "Testnet" => "000000000933ea01ad0ee984209779ba",
            "Signet" => "00000000020f01e33f91b6c7c5d3a3f8",
            "Regtest" => "0f9195cbdb894feda6ee07798e0d597d",
            "Angornet" => "00000008819873e925422c1ff0f99f7cc9bbb232af63a077a480a3633bee1ef6",
            "Liquid" => "d767f204777d8ebd0825f4f26c3d773c0d3f40268dc6afb3632a0fcbd49fde45",
            _ => throw new NotSupportedException($"Network type {network.NetworkType} is not supported"),
        };
    }

    public string GetNetworkNameFromGenesisBlockHash(string genesisBlockHash) => genesisBlockHash switch
    {
        "000000000019d6689c085ae165831e93" => "Main",
        "000000000933ea01ad0ee984209779ba" => "Testnet",
        "00000000020f01e33f91b6c7c5d3a3f8" => "Signet",
        "0f9195cbdb894feda6ee07798e0d597d" => "Regtest",
        "00000008819873e925422c1ff0f99f7cc9bbb232af63a077a480a3633bee1ef6" => "Angornet",
        "d767f204777d8ebd0825f4f26c3d773c0d3f40268dc6afb3632a0fcbd49fde45" => "Liquid",
        _ => "Unknown"
    };

    public Dictionary<string, bool> GetDefaultFeatureFlags(string network) => network switch
    {
        "Angornet" => new() { { "HW_Support", false } },
        "Liquid" => new(),
        _ => new()
    };
}
