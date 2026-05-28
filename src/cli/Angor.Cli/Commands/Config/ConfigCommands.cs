using System.CommandLine;
using System.Text.Json;
using Angor.Shared;
using Angor.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Angor.Cli.Commands.Config;

public static class ConfigCommands
{
    public static Command Build(IServiceProvider services, JsonSerializerOptions jsonOptions)
    {
        var networkConfig = services.GetRequiredService<INetworkConfiguration>();
        var networkService = services.GetRequiredService<INetworkService>();

        var cmd = new Command("config", "Configuration commands");

        cmd.AddCommand(BuildShowCommand(networkConfig, networkService, jsonOptions));
        cmd.AddCommand(BuildGetNetworkCommand(networkConfig));
        cmd.AddCommand(BuildSetNetworkCommand(networkService));
        cmd.AddCommand(BuildGetDebugModeCommand(networkConfig));
        cmd.AddCommand(BuildSetDebugModeCommand(networkConfig));

        return cmd;
    }

    private static Command BuildShowCommand(INetworkConfiguration networkConfig, INetworkService networkService, JsonSerializerOptions jsonOptions)
    {
        var jsonOption = new Option<bool>("--json", "Output as JSON");

        var cmd = new Command("show", "Show current configuration") { jsonOption };
        cmd.SetHandler((bool json) =>
        {
            var network = networkConfig.GetNetwork();
            var indexer = networkService.GetPrimaryIndexer();
            var explorer = networkService.GetPrimaryExplorer();
            var relay = networkService.GetPrimaryRelay();

            if (json)
            {
                var config = new
                {
                    network = network.Name,
                    indexer = indexer?.Url,
                    explorer = explorer?.Url,
                    relay = relay?.Url,
                    relays = networkService.GetRelays()?.Select(r => r.Url).ToList()
                };
                Console.WriteLine(JsonSerializer.Serialize(config, jsonOptions));
                return;
            }

            Console.WriteLine($"Network:   {network.Name}");
            Console.WriteLine($"Indexer:   {indexer?.Url ?? "(none)"}");
            Console.WriteLine($"Explorer:  {explorer?.Url ?? "(none)"}");
            Console.WriteLine($"Relay:     {relay?.Url ?? "(none)"}");

            var relays = networkService.GetRelays();
            if (relays?.Count > 1)
            {
                Console.WriteLine("All relays:");
                foreach (var r in relays)
                {
                    Console.WriteLine($"  {r.Url}");
                }
            }
        }, jsonOption);
        return cmd;
    }

    private static Command BuildGetNetworkCommand(INetworkConfiguration networkConfig)
    {
        var cmd = new Command("get-network", "Show current network");
        cmd.SetHandler(() =>
        {
            Console.WriteLine(networkConfig.GetNetwork().Name);
        });
        return cmd;
    }

    private static Command BuildSetNetworkCommand(INetworkService networkService)
    {
        var networkArg = new Argument<string>("network", "Network name (testnet or mainnet)");

        var cmd = new Command("set-network", "Switch network (takes effect on next run)") { networkArg };
        cmd.SetHandler((string network) =>
        {
            networkService.CheckAndSetNetwork(string.Empty, network);
            Console.WriteLine($"Network set to: {network}");
            Console.WriteLine("Restart the CLI for the change to take effect.");
        }, networkArg);
        return cmd;
    }

    private static Command BuildGetDebugModeCommand(INetworkConfiguration networkConfig)
    {
        var cmd = new Command("get-debug-mode", "Show current debug mode status");
        cmd.SetHandler(() =>
        {
            Console.WriteLine($"Debug mode: {(networkConfig.GetDebugMode() ? "enabled" : "disabled")}");
        });
        return cmd;
    }

    private static Command BuildSetDebugModeCommand(INetworkConfiguration networkConfig)
    {
        var enabledArg = new Argument<bool>("enabled", "true to enable, false to disable");

        var cmd = new Command("set-debug-mode", "Enable or disable debug mode (testnet only: stages immediately claimable)") { enabledArg };
        cmd.SetHandler((bool enabled) =>
        {
            networkConfig.SetDebugMode(enabled);
            Console.WriteLine($"Debug mode {(enabled ? "enabled" : "disabled")}.");
        }, enabledArg);
        return cmd;
    }
}
