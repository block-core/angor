using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Integration.WalletFunding;
using Angor.Contexts.Wallet;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Shared;
using Angor.Shared.Services;
using AngorApp.Composition.Registrations;
using AngorApp.Sections;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Settings;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Zafiro.Avalonia.Services;
using Zafiro.UI;
using Zafiro.UI.Navigation;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.Composition;

public static class CompositionRoot
{
    public static IMainViewModel CreateMainViewModel(Control topLevelView)
    {
        var services = new ServiceCollection();

        var logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Debug().CreateLogger();

        var store = new FileStore("Angor");
        var networkStorage = new NetworkStorage(store);
        var network = networkStorage.GetNetwork() switch
        {
            "Mainnet" => BitcoinNetwork.Mainnet,
            _ => BitcoinNetwork.Testnet
        };

        RegisterLogger(services, logger);
        services.AddKeyedSingleton<IStore>("file", store);
        services.AddSingleton<IStore>(provider => provider.GetKeyedService<IStore>("file"));

        services.AddSingleton<Func<BitcoinNetwork>>(sp => () =>
        {
            var cfg = sp.GetRequiredService<INetworkConfiguration>();
            var name = cfg.GetNetwork().Name;
            return name == "Main" || name == "Mainnet" ? BitcoinNetwork.Mainnet : BitcoinNetwork.Testnet;
        });

        ModelServices.Register(services);
        ViewModels.Register(services);
        UIServicesRegistration.Register(services, topLevelView);
        services.AddNavigator();
        SecurityContext.Register(services);
        RegisterWalletServices(services, logger, network);
        FundingContextServices.Register(services, logger);

        // Integration services
        services.AddSingleton<ISeedwordsProvider, SeedwordsProvider>();

        RegisterSections(services, logger);

        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<INetworkService>().AddSettingsIfNotExist();

        return serviceProvider.GetRequiredService<IMainViewModel>();
    }

    private static void RegisterSections(ServiceCollection services, Logger logger)
    {
        services.AddSingleton<IEnumerable<ISection>>(provider =>
        {
            var homeSection = new ContentSection<IHomeSectionViewModel>("Home", Observable.Defer(() => Observable.Return(provider.GetRequiredService<IHomeSectionViewModel>())), new Icon("svg:/Assets/angor-icon.svg"));
            
            var sq = ((bool[])[true, false]).ToObservable().Select(b => Observable.Return(b).Delay(3.Seconds())).Concat();
            sq.Subscribe(b => homeSection.IsVisible = b);
            
            return
            [
                homeSection,
                new ContentSection<IBrowseSectionViewModel>("Browse", Observable.Defer(() => Observable.Return(provider.GetRequiredService<IBrowseSectionViewModel>())), new Icon("svg:/Assets/browse.svg"))
            ];
        });

        // services.RegisterSections(builder => builder.Add(new Section())
        //         .Add<IHomeSectionViewModel>("Home", new Icon { Source = "svg:/Assets/angor-icon.svg" })
        //         .Add<Lightweight1>("Lightweight 1", new Icon { Source = "svg:/Assets/angor-icon.svg" })
        //         .Add<Lightweight2>("Lightweight 2", new Icon { Source = "svg:/Assets/angor-icon.svg" })
        //         .Separator()
        //         .Add<IWalletSectionViewModel>("Wallet", new Icon { Source = "svg:/Assets/wallet.svg" })
        //         .Add<IBrowseSectionViewModel>("Browse", new Icon { Source = "svg:/Assets/browse.svg" })
        //         .Add<IPortfolioSectionViewModel>("Portfolio",  new Icon { Source = "svg:/Assets/portfolio.svg" })
        //         .Add<IFounderSectionViewModel>("Founder", new Icon { Source = "svg:/Assets/user.svg" })
        //         .Separator()
        //         .Add<ISettingsSectionViewModel>("Settings", new Icon { Source = "svg:/Assets/settings.svg" })
        //         .Command("Angor Hub", provider => ReactiveCommand.CreateFromTask(() => provider.GetRequiredService<ILauncherService>().LaunchUri(new Uri("https://hub.angor.io"))), new Icon { Source = "svg:/Assets/browse.svg" } , false)
        //     , logger);
    }

    private static void RegisterWalletServices(ServiceCollection services, Logger logger, BitcoinNetwork network)
    {
        WalletContextServices.Register(services, logger, network);
    }

    private static void RegisterLogger(ServiceCollection services, Logger logger)
    {
        services.AddLogging(builder => builder.AddSerilog());
        services.AddSingleton<ILogger>(logger);
    }
}