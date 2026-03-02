using Angor.Data.Documents.LiteDb.Extensions;
using Angor.Sdk.Common;
using Angor.Sdk.Funding;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Wallet;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Impl;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Services;
using Avalonia2.Composition.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Avalonia2.Composition;

/// <summary>
/// Static service locator that initializes the DI container with all SDK services.
/// Provides typed access to key services throughout the Avalonia2 app.
/// Modeled after AngorApp's CompositionRoot.
/// </summary>
public static class ServiceLocator
{
    private static IServiceProvider? _provider;

    public static bool IsInitialized => _provider != null;

    /// <summary>
    /// Initialize the service container with all SDK services.
    /// Must be called once at app startup (App.OnFrameworkInitializationCompleted).
    /// </summary>
    public static void Initialize()
    {
        if (_provider != null) return;

        var services = new ServiceCollection();

        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        var applicationStorage = new Avalonia2ApplicationStorage();
        var profileContext = new ProfileContext("Avalonia2", "Default");

        var store = new FileStore(applicationStorage, profileContext);
        var networkStorage = new NetworkStorage(store);
        var network = networkStorage.GetNetwork() switch
        {
            "Mainnet" => BitcoinNetwork.Mainnet,
            _ => BitcoinNetwork.Testnet
        };

        // Core infrastructure
        services.AddSingleton<IApplicationStorage>(applicationStorage);
        services.AddLiteDbDocumentStorage(profileContext);
        services.AddKeyedSingleton<IStore>("file", store);
        services.AddSingleton<IStore>(provider => provider.GetKeyedService<IStore>("file")!);
        services.AddSingleton(logger);
        services.AddSingleton<Serilog.ILogger>(logger);

        // Network function provider
        services.AddSingleton<Func<BitcoinNetwork>>(sp => () =>
        {
            var cfg = sp.GetRequiredService<INetworkConfiguration>();
            var name = cfg.GetNetwork().Name;
            return name == "Main" || name == "Mainnet" ? BitcoinNetwork.Mainnet : BitcoinNetwork.Testnet;
        });

        // Security adapters
        services.AddSingleton<IPassphraseProvider, SimplePassphraseProvider>();
        services.AddSingleton<SimplePasswordProvider>();
        services.AddSingleton<IPasswordProvider>(sp => sp.GetRequiredService<SimplePasswordProvider>());
        services.AddSingleton<IWalletSecurityContext, WalletSecurityContext>();
        services.AddSingleton<IWalletEncryption, AesWalletEncryption>();

        // Register SDK services
        WalletContextServices.Register(services, logger, network);
        FundingContextServices.Register(services, logger);

        _provider = services.BuildServiceProvider();

        // Initialize network settings
        _provider.GetRequiredService<INetworkService>().AddSettingsIfNotExist();
    }

    /// <summary>Resolve a service from the container.</summary>
    public static T Get<T>() where T : notnull
    {
        if (_provider == null) throw new InvalidOperationException("ServiceLocator not initialized. Call Initialize() first.");
        return _provider.GetRequiredService<T>();
    }

    /// <summary>Try to resolve a service from the container.</summary>
    public static T? TryGet<T>() where T : class
    {
        return _provider?.GetService<T>();
    }

    // ── Convenience accessors for frequently used services ──

    public static IWalletAppService WalletApp => Get<IWalletAppService>();
    public static IProjectAppService ProjectApp => Get<IProjectAppService>();
    public static IInvestmentAppService InvestmentApp => Get<IInvestmentAppService>();
    public static IFounderAppService FounderApp => Get<IFounderAppService>();
    public static INetworkService NetworkService => Get<INetworkService>();
    public static INetworkConfiguration NetworkConfig => Get<INetworkConfiguration>();
    public static INetworkStorage NetworkStorage => Get<INetworkStorage>();
    public static IWalletAccountBalanceService BalanceService => Get<IWalletAccountBalanceService>();
}
