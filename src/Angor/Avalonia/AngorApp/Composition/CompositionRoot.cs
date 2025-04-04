using System.Linq;
using Angor.Projects;
using Angor.Projects.Infrastructure;
using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure;
using Angor.Wallet.Infrastructure.Impl;
using Angor.Wallet.Infrastructure.Interfaces;
using AngorApp.Composition.Registrations;
using AngorApp.Sections.Shell;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;

namespace AngorApp.Composition;

public static class CompositionRoot
{
    public static IMainViewModel CreateMainViewModel(Control topLevelView)
    {
        var services = new ServiceCollection();

        var logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        RegisterLogger(services, logger);
        services.AddSingleton<Func<BitcoinNetwork>>(() => BitcoinNetwork.Testnet);

        ModelServices.Register(services);
        ViewModels.Register(services);
        UIServices.Register(services, topLevelView);
        SecurityContext.Register(services);
        RegisterWalletServices(services, logger);
        ProjectServices.Register(services, logger);

        var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<IMainViewModel>();
    }

    private static void RegisterWalletServices(ServiceCollection services, Logger logger)
    {
        // TODO: Set network from configuration
        WalletServices.Register(services, logger, BitcoinNetwork.Testnet)
            .AddSingleton<IStore>(new FileStore("Angor"));
    }

    private static void RegisterLogger(ServiceCollection services, Logger logger)
    {
        services.AddLogging(builder => builder.AddSerilog());
        services.AddSingleton<ILogger>(logger);
    }
}