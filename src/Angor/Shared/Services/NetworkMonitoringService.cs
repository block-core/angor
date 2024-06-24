using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Angor.Shared.Services;

public class NetworkMonitoringService : BackgroundService
{
    private readonly INetworkService _networkService;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public NetworkMonitoringService(INetworkService networkService)
    {
        _networkService = networkService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _networkService.CheckServices(true);

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
