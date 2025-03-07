using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure;
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

        AngorServices.Register(services);
        ModelServices.Register(services);
        ViewModels.Register(services);
        UIServices.Register(services, topLevelView);
        SecurityContext.Register(services);
        WalletServices.Register(services, logger);

        var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<IMainViewModel>();
    }

    private static void RegisterLogger(ServiceCollection services, Logger logger)
    {
        services.AddLogging(builder => builder.AddSerilog());
        services.AddSingleton<ILogger>(logger);
    }
}