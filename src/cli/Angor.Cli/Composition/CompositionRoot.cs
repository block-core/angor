using Angor.Data.Documents.LiteDb.Extensions;
using Angor.Sdk.Common;
using Angor.Sdk.Funding;
using Angor.Sdk.Integration;
using Angor.Sdk.Wallet;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Impl;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Angor.Cli.Composition;

/// <summary>
/// Headless DI container for the CLI/MCP server.
/// Mirrors the design app's CompositionRoot but strips all UI dependencies.
/// </summary>
public static class CompositionRoot
{
    public static IServiceProvider BuildServiceProvider(bool isMcpMode, string profileName = "Default")
    {
        var services = new ServiceCollection();

        var applicationStorage = new CliApplicationStorage();
        var profileContext = new ProfileContext("Angor", applicationStorage.SanitizeProfileName(profileName));

        var store = new FileStore(applicationStorage, profileContext);
        var networkStorage = new NetworkStorage(store);
        var network = networkStorage.GetNetwork() switch
        {
            "Mainnet" => BitcoinNetwork.Mainnet,
            _ => BitcoinNetwork.Testnet
        };

        // Logging — file only (console is reserved for CLI output / MCP stdio)
        var logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Angor", "logs", "angor-cli-.log");

        services.AddLogging(builder =>
        {
            builder.ClearProviders();

            var fileLogger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 15,
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            builder.AddSerilog(fileLogger, dispose: true);

            // Suppress noisy HTTP diagnostics
            builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            builder.AddFilter("Angor.Shared.WalletOperations", LogLevel.Warning);
            builder.AddFilter("Angor.Shared.DerivationOperations", LogLevel.Warning);
        });

        // Minimal Serilog logger required by SDK Register methods (parameter signature)
        var serilogLogger = new LoggerConfiguration().CreateLogger();

        // Core infrastructure
        services.AddSingleton<IApplicationStorage>(applicationStorage);
        services.AddSingleton(profileContext);
        services.AddLiteDbDocumentStorage(profileContext);
        services.AddKeyedSingleton<IStore>("file", store);
        services.AddSingleton<IStore>(provider => provider.GetKeyedService<IStore>("file")!);

        // Network function provider
        services.AddSingleton<Func<BitcoinNetwork>>(sp => () =>
        {
            var cfg = sp.GetRequiredService<INetworkConfiguration>();
            var name = cfg.GetNetwork().Name;
            return name == "Main" || name == "Mainnet" ? BitcoinNetwork.Mainnet : BitcoinNetwork.Testnet;
        });

        // Security — headless implementations
        services.AddSingleton<IPassphraseProvider>(new HeadlessPassphraseProvider());
        services.AddSingleton<IPasswordProvider>(new HeadlessPasswordProvider(isMcpMode));
        services.AddSingleton<IWalletSecurityContext, WalletSecurityContext>();
        services.AddSingleton<IWalletEncryption, AesWalletEncryption>();

        // Platform-specific secure key storage
        if (OperatingSystem.IsWindows())
            services.AddSingleton<ISecureKeyProvider, DpapiSecureKeyProvider>();
        else if (OperatingSystem.IsLinux())
            services.AddSingleton<ISecureKeyProvider, LinuxSecureKeyProvider>();
        else if (OperatingSystem.IsMacOS())
            services.AddSingleton<ISecureKeyProvider, LinuxSecureKeyProvider>(); // Fallback for macOS
        else
            throw new PlatformNotSupportedException("No ISecureKeyProvider implementation available for this platform.");

        // Register SDK services
        WalletContextServices.Register(services, serilogLogger, network);
        FundingContextServices.Register(services, serilogLogger);
        services.AddSingleton<ISeedwordsProvider, SeedwordsProvider>();

        // HTTP client factory
        services.AddHttpClient();

        var provider = services.BuildServiceProvider();

        // Initialize network settings
        provider.GetRequiredService<INetworkService>().AddSettingsIfNotExist();

        return provider;
    }
}
