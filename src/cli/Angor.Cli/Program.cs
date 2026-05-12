using System.CommandLine;
using System.Text.Json;
using Angor.Cli.Commands.Wallet;
using Angor.Cli.Commands.Projects;
using Angor.Cli.Composition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace Angor.Cli;

public class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--mcp"))
        {
            return await RunMcpServer(args);
        }

        return await RunCli(args);
    }

    private static async Task<int> RunCli(string[] args)
    {
        var rootCommand = new RootCommand("Angor CLI — Bitcoin investment platform command-line tool");

        // Build DI container
        var serviceProvider = CompositionRoot.BuildServiceProvider(isMcpMode: false);

        // Register command groups
        rootCommand.AddCommand(WalletCommands.Build(serviceProvider, JsonOptions));
        rootCommand.AddCommand(ProjectCommands.Build(serviceProvider, JsonOptions));

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> RunMcpServer(string[] args)
    {
        var serviceProvider = CompositionRoot.BuildServiceProvider(isMcpMode: true);

        var builder = Host.CreateApplicationBuilder();

        // Suppress console logging in MCP mode (stdout is reserved for JSON-RPC)
        builder.Logging.ClearProviders();

        // Register all SDK services into the host's DI so MCP tools can resolve them.
        builder.Services.AddSingleton(serviceProvider.GetRequiredService<Angor.Sdk.Wallet.Application.IWalletAppService>());
        builder.Services.AddSingleton(serviceProvider.GetRequiredService<Angor.Sdk.Funding.Projects.IProjectAppService>());
        builder.Services.AddSingleton(serviceProvider.GetRequiredService<Angor.Sdk.Funding.Founder.IFounderAppService>());
        builder.Services.AddSingleton(serviceProvider.GetRequiredService<Angor.Sdk.Funding.Investor.IInvestmentAppService>());
        builder.Services.AddSingleton(serviceProvider.GetRequiredService<Angor.Shared.INetworkConfiguration>());
        builder.Services.AddSingleton(serviceProvider.GetRequiredService<Angor.Shared.Services.INetworkService>());
        builder.Services.AddSingleton(serviceProvider.GetRequiredService<Angor.Sdk.Common.IStore>());

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
        return 0;
    }
}
