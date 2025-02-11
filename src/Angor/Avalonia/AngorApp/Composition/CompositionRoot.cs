using AngorApp.Sections.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace AngorApp.Composition;

public static class CompositionRoot
{
    public static IMainViewModel CreateMainViewModel(Control topLevelView)
    {
        var services = new ServiceCollection();

        services
            .AddUIServices(topLevelView)
            .AddUIModelServices()
            .AddViewModels();

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IMainViewModel>();
    }
}