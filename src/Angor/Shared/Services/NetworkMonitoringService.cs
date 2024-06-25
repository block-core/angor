using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Angor.Shared.Services;

public class NetworkMonitoringService : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private Task _task;
    private CancellationTokenSource _cancellationTokenSource;
    private TimeSpan _checkInterval;
    private bool _isRunning;

    public NetworkMonitoringService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _checkInterval = TimeSpan.FromMinutes(1); // Default check interval
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public void Start()
    {
        if (!_isRunning)
        {
            _isRunning = true;
            _task = Task.Run(async () => await RunPeriodicCheck(), _cancellationTokenSource.Token);
        }
    }

    private async Task RunPeriodicCheck()
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                await CheckServices();
                await Task.Delay(_checkInterval, _cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                break; 
            }
            catch (Exception ex)
            {
                AdjustIntervalOnError(); 
            }
        }
    }

    private async Task CheckServices()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var networkService = scope.ServiceProvider.GetRequiredService<INetworkService>();
            try
            {
                networkService.AddSettingsIfNotExist();
                await networkService.CheckServices(true);
                AdjustInterval(true);
            }
            catch (Exception ex)
            {
                AdjustInterval(false);
            }
        }
    }

    private void AdjustInterval(bool isSuccess)
    {
        if (!isSuccess)
        {
            _checkInterval = TimeSpan.FromMinutes(5);
        }
        else
        {
            _checkInterval = TimeSpan.FromMinutes(1);
        }
    }

    private void AdjustIntervalOnError()
    {
        _checkInterval = TimeSpan.FromMinutes(10);
    }

    public void Stop()
    {
        if (_isRunning)
        {
            _cancellationTokenSource.Cancel();
            _task.Wait();
            _isRunning = false;
        }
    }

    public void Dispose()
    {
        if (_isRunning)
        {
            Stop();
        }
        _cancellationTokenSource.Dispose();
    }
}
