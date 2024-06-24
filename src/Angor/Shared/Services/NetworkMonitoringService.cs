using System;
using System.Threading;
using System.Threading.Tasks;
 using Angor.Shared.Services;

public class NetworkMonitoringService : IDisposable
{
    private readonly INetworkService _networkService;
    private Timer _timer;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public NetworkMonitoringService(INetworkService networkService)
    {
        _networkService = networkService;
    }

    public void Start()
    {
         _timer = new Timer(async _ => await CheckServices(), null, TimeSpan.Zero, _checkInterval);
    }

    private async Task CheckServices()
    {
        _networkService.AddSettingsIfNotExist();

         await _networkService.CheckServices(true);
     }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
