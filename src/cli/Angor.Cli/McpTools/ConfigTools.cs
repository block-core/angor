using System.ComponentModel;
using System.Text.Json;
using Angor.Shared;
using Angor.Shared.Services;
using ModelContextProtocol.Server;

namespace Angor.Cli.McpTools;

[McpServerToolType]
public class ConfigTools(INetworkConfiguration networkConfig, INetworkService networkService)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool, Description("Show current configuration including network, indexer, explorer, and relay URLs")]
    public string ConfigShow()
    {
        var config = new
        {
            network = networkConfig.GetNetwork().Name,
            indexer = networkService.GetPrimaryIndexer()?.Url,
            explorer = networkService.GetPrimaryExplorer()?.Url,
            relay = networkService.GetPrimaryRelay()?.Url,
            relays = networkService.GetRelays()?.Select(r => r.Url).ToList()
        };
        return JsonSerializer.Serialize(config, JsonOptions);
    }

    [McpServerTool, Description("Get the current network name (testnet or mainnet)")]
    public string ConfigGetNetwork()
    {
        return networkConfig.GetNetwork().Name;
    }

    [McpServerTool, Description("Switch network. Takes effect on next CLI/MCP restart.")]
    public string ConfigSetNetwork(string network)
    {
        networkService.CheckAndSetNetwork(string.Empty, network);
        return $"Network set to: {network}. Restart for the change to take effect.";
    }

    [McpServerTool, Description("Get current debug mode status. When debug mode is enabled on testnet, investment windows are always open and stages are immediately claimable.")]
    public string ConfigGetDebugMode()
    {
        return JsonSerializer.Serialize(new { debugMode = networkConfig.GetDebugMode() }, JsonOptions);
    }

    [McpServerTool, Description("Enable or disable debug mode. When enabled on testnet, investment windows are always open and stages are immediately claimable. Takes effect immediately.")]
    public string ConfigSetDebugMode(bool enabled)
    {
        networkConfig.SetDebugMode(enabled);
        return $"Debug mode {(enabled ? "enabled" : "disabled")}.";
    }
}
