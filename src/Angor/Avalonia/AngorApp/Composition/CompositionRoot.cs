using Angor.Contexts.Funding;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Integration.WalletFunding;
using Angor.Contexts.Wallet;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using AngorApp.Composition.Registrations;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Zafiro.UI.Navigation;

namespace AngorApp.Composition;

public static class CompositionRoot
{
    public static IMainViewModel CreateMainViewModel(Control topLevelView)
    {
        var services = new ServiceCollection();

        var logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Debug().CreateLogger();
        
        RegisterLogger(services, logger);
        services.AddSingleton<Func<BitcoinNetwork>>(() => BitcoinNetwork.Testnet);

        ModelServices.Register(services);
        ViewModels.Register(services);
        UIServicesRegistration.Register(services, topLevelView);
        SecurityContext.Register(services);
        RegisterWalletServices(services, logger);
        FundingContextServices.Register(services, logger);

        // Integration services
        services.AddSingleton<ISeedwordsProvider, SeedwordsProvider>();
        
        RegisterSections(services);

        var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<IMainViewModel>();
    }

    private static void RegisterSections(ServiceCollection services)
    {
        services.RegisterSections(builder => builder
            .Add<IHomeSectionViewModel>("Home", "svg:/Assets/angor-icon.svg")
            .Separator()
            .Add<IWalletSectionViewModel>("Wallet", "fa-wallet")
            .Add<IBrowseSectionViewModel>("Browse", "fa-magnifying-glass")
            .Add<IPortfolioSectionViewModel>("Portfolio", "fa-hand-holding-dollar")
            .Add<IFounderSectionViewModel>("Founder", "fa-money-bills")
            .Command("Angor Hub", ReactiveCommand.Create(() => { }), "fa-gear", false)
        );
    }

    private static void RegisterWalletServices(ServiceCollection services, Logger logger)
    {
        // TODO: Set network from configuration
        WalletContextServices.Register(services, logger, BitcoinNetwork.Testnet)
            .AddSingleton<IStore>(new FileStore("Angor"));
    }

    private static void RegisterLogger(ServiceCollection services, Logger logger)
    {
        services.AddLogging(builder => builder.AddSerilog());
        services.AddSingleton<ILogger>(logger);
    }
}