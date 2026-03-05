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
using Avalonia2.UI.Sections.FindProjects;
using Avalonia2.UI.Sections.Funders;
using Avalonia2.UI.Sections.Funds;
using Avalonia2.UI.Sections.Home;
using Avalonia2.UI.Sections.MyProjects;
using Avalonia2.UI.Sections.MyProjects.Deploy;
using Avalonia2.UI.Sections.Portfolio;
using Avalonia2.UI.Sections.Settings;
using Avalonia2.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Avalonia2.Composition;

/// <summary>
/// Static factory that builds the DI container with all SDK services and ViewModel registrations.
/// Replaces the former ServiceLocator.
/// </summary>
public static class CompositionRoot
{
    public static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        var applicationStorage = new Avalonia2ApplicationStorage();
        var profileContext = new ProfileContext("Avalonia2", "Default");

        var store = new FileStore(applicationStorage, profileContext);
        var networkStorage = new NetworkStorage(store);
        var network = networkStorage.GetNetwork() switch
        {
            "Mainnet" => BitcoinNetwork.Mainnet,
            _ => BitcoinNetwork.Testnet
        };

        // Logging — Microsoft.Extensions.Logging with console output
        services.AddLogging(builder => builder.AddConsole());

        // Minimal Serilog logger required by SDK Register methods (parameter signature)
        var serilogLogger = new LoggerConfiguration().CreateLogger();

        // Core infrastructure
        services.AddSingleton<IApplicationStorage>(applicationStorage);
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

        // ── Shared singletons (replaces SharedViewModels static class) ──
        services.AddSingleton<SignatureStore>();
        services.AddSingleton<PrototypeSettings>();
        services.AddSingleton<PortfolioViewModel>();

        // ── Section VMs (transient — fresh per navigation) ──
        services.AddTransient<ShellViewModel>();
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
                sp.GetRequiredService<IInvestmentAppService>()));

        services.AddSingleton<Func<MyProjectItemViewModel, ManageProjectViewModel>>(sp =>
            project => new ManageProjectViewModel(
                project,
                sp.GetRequiredService<IFounderAppService>()));

        var provider = services.BuildServiceProvider();

        // Initialize network settings
        provider.GetRequiredService<INetworkService>().AddSettingsIfNotExist();

        return provider;
    }
}
