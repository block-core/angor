using AngorApp.Composition.Registrations;
using AngorApp.Sections.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace AngorApp.Composition;

public static class CompositionRoot
{
    public static IMainViewModel CreateMainViewModel(Control topLevelView)
    {
        var services = new ServiceCollection();

        AngorServices.Register(services);
        ModelServices.Register(services);
        ViewModels.Register(services);
        UIServices.Register(services, topLevelView);
        WalletServices.Register(services);

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IMainViewModel>();
    }
}