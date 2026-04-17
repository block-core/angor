using Angor.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services;

/// <summary>
/// Delegates to <see cref="IAddressPollingService"/> for the core polling logic,
/// but preserves the original exception-throwing contract for backward compatibility:
/// throws <see cref="OperationCanceledException"/> on cancellation and
/// <see cref="TimeoutException"/> on timeout.
/// </summary>
public class MempoolMonitoringService : IMempoolMonitoringService
{
    private readonly IAddressPollingService _addressPollingService;
    private readonly ILogger<MempoolMonitoringService> _logger;

    // Hardcoded configuration - will be moved to configuration later
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(2);

    internal MempoolMonitoringService(
        IIndexerService indexerService,
        ILogger<MempoolMonitoringService> logger)
        : this(new AddressPollingService(indexerService, new Logger<AddressPollingService>(new LoggerFactory())), logger)
    {
    }

    public MempoolMonitoringService(
        IAddressPollingService addressPollingService,
        ILogger<MempoolMonitoringService> logger)
    {
        _addressPollingService = addressPollingService;
        _logger = logger;
    }

    public async Task<List<UtxoData>> MonitorAddressForFundsAsync(
        string address, 
        long requiredAmount, 
        TimeSpan timeout, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting mempool monitoring for address {Address}, required amount: {RequiredAmount} sats", 
            address, requiredAmount);

        // Delegate to the shared polling service
        var result = await _addressPollingService.WaitForFundsAsync(
            address, requiredAmount, timeout, _pollingInterval, cancellationToken);

        // Preserve the original exception-throwing contract:
        // - If cancelled and no funds found, throw OperationCanceledException
        // - If timed out and no funds found, throw TimeoutException
        if (result.Count == 0)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Mempool monitoring canceled for address {Address}", address);
                throw new OperationCanceledException("Mempool monitoring was canceled");
            }

            _logger.LogWarning("Mempool monitoring timeout for address {Address}. Required amount: {RequiredAmount} sats not received", 
                address, requiredAmount);
            throw new TimeoutException($"Timeout waiting for funds on address {address}. Required: {requiredAmount} sats");
        }

        return result;
    }
}

