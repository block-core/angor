using Angor.Shared.Services;
using Angor.UI.Model.Implementation;
using AngorApp.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AngorApp.Composition.Registrations;

public static class AngorServices
{
    public static IServiceCollection Register(this IServiceCollection services)
    {
        return services
            .AddSingleton<IIndexerService>(sp => DependencyFactory.GetIndexerService(sp.GetRequiredService<ILoggerFactory>()))
            .AddSingleton<IRelayService>(sp => DependencyFactory.GetRelayService(sp.GetRequiredService<ILoggerFactory>()));
    }
}