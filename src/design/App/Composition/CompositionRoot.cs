using Angor.Data.Documents.LiteDb.Extensions;
using Angor.Sdk.Common;
using Angor.Sdk.Funding;
using Angor.Sdk.Integration;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Wallet;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Impl;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Integration.Lightning;
using Angor.Shared.Services;
using App.Composition.Adapters;
using App.UI.Sections.FindProjects;
using App.UI.Sections.Funders;
using App.UI.Sections.Funds;
using App.UI.Sections.Home;
using App.UI.Sections.MyProjects;
using App.UI.Sections.MyProjects.Deploy;
using App.UI.Sections.MyProjects.EditProfile;
using App.UI.Sections.Portfolio;
using App.UI.Sections.Settings;
using App.UI.Shared;
using App.UI.Shared.Services;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace App.Composition;

/// <summary>
/// Static factory that builds the DI container with all SDK services and ViewModel registrations.
/// Replaces the former ServiceLocator.
/// </summary>
public static class CompositionRoot
{
    public static IServiceProvider BuildServiceProvider(string profileName = "Default", bool enableConsoleLogging = true)
    {
        var services = new ServiceCollection();

        var applicationStorage = new AppApplicationStorage();
        var profileContext = new ProfileContext("Angor", applicationStorage.SanitizeProfileName(profileName));

        var store = new FileStore(applicationStorage, profileContext);
        var networkStorage = new NetworkStorage(store);
        var network = networkStorage.GetNetwork() switch
        {
            "Mainnet" => BitcoinNetwork.Mainnet,
            _ => BitcoinNetwork.Testnet
        };

        // Logging — Microsoft.Extensions.Logging with console + file output
        var logFilePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Angor", "logs", "angor-.log");

        services.AddLogging(builder =>
        {
            builder.ClearProviders();

            if (enableConsoleLogging)
            {
                builder.AddConsole();
            }

            // Add Serilog as a provider for file logging
            var fileLogger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: Serilog.RollingInterval.Day,
                    retainedFileCountLimit: 15,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            builder.AddSerilog(fileLogger, dispose: true);

            // Suppress noisy per-request HTTP diagnostics (Sending/Received for every call)
            builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            // Suppress verbose per-address balance/utxo and derivation logs
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

        // Security adapters
        services.AddSingleton<IPassphraseProvider, SimplePassphraseProvider>();
        services.AddSingleton<SimplePasswordProvider>();
        services.AddSingleton<IPasswordProvider>(sp => sp.GetRequiredService<SimplePasswordProvider>());
        services.AddSingleton<IWalletSecurityContext, WalletSecurityContext>();
        services.AddSingleton<IWalletEncryption, AesWalletEncryption>();

        // Register SDK services
        WalletContextServices.Register(services, serilogLogger, network);
        FundingContextServices.Register(services, serilogLogger);
        services.AddSingleton<ISeedwordsProvider, SeedwordsProvider>();

        // Currency symbol service — reads ticker from INetworkConfiguration
        services.AddSingleton<ICurrencyService, CurrencyService>();

        // INetworkStorage override for integration tests that need to point at a local
        // docker stack (see src/design/App.Test.Integration/docker). When ANGOR_INDEXER_URL
        // or ANGOR_RELAY_URLS is set, the decorator returns the env-var values from
        // GetSettings() but never persists them, so the underlying database stays clean.
        if (EnvOverrideNetworkStorage.IsActive())
        {
            services.AddSingleton<INetworkStorage>(sp =>
                new EnvOverrideNetworkStorage(new NetworkStorage(sp.GetRequiredService<IStore>())));
        }

        // Faucet service — integration tests replace this registration to point
        // at the local docker faucet (see src/design/App.Test.Integration/docker).
        // Env-var override:
        //   ANGOR_FAUCET_BASE_URL   e.g. http://localhost:48500
        //   ANGOR_FAUCET_SEND_PATH  e.g. api/send/{0}/{1}   (defaults to the bitcoin-custom-signet path)
        services.AddHttpClient();
        services.AddSingleton(ResolveFaucetOptions());
        services.AddSingleton<IFaucetService, HttpFaucetService>();

        // ── Shared singletons (replaces SharedViewModels static class) ──
        services.AddSingleton<SignatureStore>();
        services.AddSingleton<PrototypeSettings>();
        services.AddSingleton<IWalletContext, WalletContext>();
        services.AddSingleton<PortfolioViewModel>();

        // ── Section VMs (transient — fresh per navigation) ──
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<FundsViewModel>();
        services.AddTransient<FindProjectsViewModel>();
        services.AddTransient<MyProjectsViewModel>();
        services.AddTransient<FundersViewModel>();
        services.AddTransient<CreateProjectViewModel>();
        services.AddTransient<DeployFlowViewModel>();
        services.AddTransient<HomeViewModel>();

        // ── Factory delegates for VMs created with runtime data ──
        services.AddSingleton<Func<ProjectItemViewModel, InvestPageViewModel>>(sp =>
            project => new InvestPageViewModel(
                project,
                sp.GetRequiredService<IWalletAppService>(),
                sp.GetRequiredService<IInvestmentAppService>(),
                sp.GetRequiredService<IBoltzSwapService>(),
                sp.GetRequiredService<PortfolioViewModel>(),
                sp.GetRequiredService<ICurrencyService>(),
                sp.GetRequiredService<IWalletContext>(),
                sp.GetRequiredService<Func<BitcoinNetwork>>(),
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<InvestPageViewModel>()));

        services.AddSingleton<Func<MyProjectItemViewModel, ManageProjectViewModel>>(sp =>
            project => new ManageProjectViewModel(
                project,
                sp.GetRequiredService<IFounderAppService>(),
                sp.GetRequiredService<IProjectAppService>(),
                sp.GetRequiredService<IProjectService>(),
                sp.GetRequiredService<ICurrencyService>(),
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<ManageProjectViewModel>()));

        services.AddSingleton<Func<MyProjectItemViewModel, EditProfileViewModel>>(sp =>
            project => new EditProfileViewModel(
                project,
                sp.GetRequiredService<IProjectAppService>(),
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<EditProfileViewModel>()));

        // ── Section Views (transient — each receives its VM via constructor injection) ──
        services.AddTransient<HomeView>();
        services.AddTransient<FundsView>();
        services.AddTransient<FindProjectsView>();
        services.AddTransient<PortfolioView>();
        services.AddTransient<MyProjectsView>();
        services.AddTransient<FundersView>();
        services.AddTransient<SettingsView>();

        // ── View factory — maps section keys to DI-resolved Views ──
        services.AddSingleton<Func<string, object?>>(sp => key => key switch
        {
            "Home" => sp.GetRequiredService<HomeView>(),
            "Funds" => sp.GetRequiredService<FundsView>(),
            "Find Projects" => sp.GetRequiredService<FindProjectsView>(),
            "Funded" => sp.GetRequiredService<PortfolioView>(),
            "My Projects" => sp.GetRequiredService<MyProjectsView>(),
            "Funders" => sp.GetRequiredService<FundersView>(),
            "Settings" => sp.GetRequiredService<SettingsView>(),
            _ => null,
        });

        var provider = services.BuildServiceProvider();

        // Initialize network settings
        provider.GetRequiredService<INetworkService>().AddSettingsIfNotExist();

        // Sync persisted debug mode into INetworkConfiguration so it's available immediately
        // (SettingsViewModel also does this in its constructor, but it's transient and only
        // created when the user navigates to Settings)
        var prototypeSettings = provider.GetRequiredService<PrototypeSettings>();
        var networkConfig = provider.GetRequiredService<INetworkConfiguration>();
        networkConfig.SetDebugMode(prototypeSettings.IsDebugMode);

        // Load persisted Find Projects cache on a background thread so the first
        // Find Projects tap seeds from disk instantly. Cheap and non-contending.
        // The full Latest() fetch runs when the user actually opens Find Projects.
        _ = Task.Run(async () =>
        {
            var prewarmLogger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Prewarm");
            try
            {
                await FindProjectsViewModel.LoadCachedDtosFromDiskAsync(
                    provider.GetRequiredService<IStore>(), prewarmLogger);
            }
            catch (Exception ex)
            {
                prewarmLogger.LogWarning(ex, "Disk cache load failed");
            }
        });

        return provider;
    }

    private static FaucetOptions ResolveFaucetOptions()
    {
        var baseUrl = Environment.GetEnvironmentVariable("ANGOR_FAUCET_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return FaucetOptions.AngorPublic;
        }

        var sendPath = Environment.GetEnvironmentVariable("ANGOR_FAUCET_SEND_PATH");
        if (string.IsNullOrWhiteSpace(sendPath))
        {
            // The bitcoin-custom-signet faucet-api and production faucet share
            // the same route surface, so the default matches FaucetOptions.AngorPublic.
            sendPath = FaucetOptions.AngorPublic.SendPathTemplate;
        }

        return new FaucetOptions(baseUrl, sendPath);
    }
}
