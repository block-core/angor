using Angor.Shared.Services.Indexer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services.Indexer.Electrum;

/// <summary>
/// Extension methods for registering Electrum services in DI container.
/// </summary>
public static class ElectrumServiceExtensions
{
    /// <summary>
    /// Adds Electrum-based indexer services to the service collection.
    /// This replaces the HTTP-based MempoolSpaceIndexerApi with Electrum protocol services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serverConfigs">Optional list of Electrum server configurations. If not provided, uses default configurations.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddElectrumServices(
        this IServiceCollection services,
        IEnumerable<ElectrumServerConfig>? serverConfigs = null)
    {
        // Use default servers if none provided
        var configs = serverConfigs?.ToList() ?? GetDefaultServerConfigs();

        // Register the client pool as singleton (manages connections)
        services.AddSingleton(sp =>
             {
                 var logger = sp.GetRequiredService<ILogger<ElectrumClientPool>>();
                 var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                 return new ElectrumClientPool(logger, loggerFactory, configs);
             });

        // Register Electrum-based IIndexerService
        services.AddSingleton<IIndexerService, ElectrumIndexerService>();

        // Register Electrum-based IAngorIndexerService
        services.AddSingleton<IAngorIndexerService, ElectrumAngorIndexerService>();

        // Ensure MempoolIndexerMappers is registered (needed by ElectrumAngorIndexerService)
        services.TryAddSingleton<MempoolIndexerMappers>();

        return services;
    }

    /// <summary>
    /// Adds Electrum services configured with a single server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="host">Electrum server hostname.</param>
    /// <param name="port">Electrum server port (default: 50002 for SSL).</param>
    /// <param name="useSsl">Whether to use SSL (default: true).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddElectrumServices(
          this IServiceCollection services,
        string host,
        int port = 50002,
          bool useSsl = true)
    {
        var config = new ElectrumServerConfig
        {
            Host = host,
            Port = port,
            UseSsl = useSsl
        };

        return services.AddElectrumServices(new[] { config });
    }

    /// <summary>
    /// Gets default Electrum server configurations for common networks.
    /// </summary>
    private static List<ElectrumServerConfig> GetDefaultServerConfigs()
    {
        // These are well-known public Electrum servers
        // Users should configure their own servers for production use
        return new List<ElectrumServerConfig>
        {
        // Blockstream's Electrum server (mainnet)
            new()
            {
                Host = "electrum.blockstream.info",
                Port = 50002,
                UseSsl = true,
                Network = "mainnet"
            },
            // Blockstream's Electrum server (testnet)
            new()
            {
                Host = "electrum.blockstream.info",
                Port = 60002,
                UseSsl = true,
                Network = "testnet"
            }
        };
    }
}
