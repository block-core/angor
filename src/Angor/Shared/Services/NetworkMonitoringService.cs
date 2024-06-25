using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Angor.Shared.Services;

public class NetworkMonitoringService : IDisposable
{
    private readonly IServiceProvider _serviceProvider;

    public NetworkMonitoringService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task CheckAndEnsureServices()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var networkService = scope.ServiceProvider.GetRequiredService<INetworkService>();
            try
            {
                networkService.AddSettingsIfNotExist();
                await networkService.CheckServices(true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error checking services: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
    }
}
