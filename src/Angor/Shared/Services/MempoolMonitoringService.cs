using Angor.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services;

public class MempoolMonitoringService : IMempoolMonitoringService
{
    private readonly IIndexerService _indexerService;
    private readonly ILogger<MempoolMonitoringService> _logger;

    // Hardcoded configuration - will be moved to configuration later
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);

    public MempoolMonitoringService(
        IIndexerService indexerService,
        ILogger<MempoolMonitoringService> logger)
    {
        _indexerService = indexerService;
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

        var startTime = DateTime.UtcNow;
        var endTime = startTime.Add(timeout);

        while (DateTime.UtcNow < endTime)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Mempool monitoring cancelled for address {Address}", address);
                throw new OperationCanceledException("Mempool monitoring was cancelled");
            }

            try
            {
                // Fetch UTXOs for the address (including mempool transactions)
                var utxos = await _indexerService.FetchUtxoAsync(address, limit: 50, offset: 0);

                if (utxos != null && utxos.Any())
                {
                    // Filter for unconfirmed transactions (mempool)
                    var mempoolUtxos = utxos
                        .Where(u => u.blockIndex == 0) // blockIndex == 0 means unconfirmed (in mempool)
                        .ToList();

                    if (mempoolUtxos.Any())
                    {
                        var totalAmount = mempoolUtxos.Sum(u => u.value);
                        
                        _logger.LogInformation("Detected {Count} mempool UTXO(s) for address {Address}, total amount: {TotalAmount} sats", 
                            mempoolUtxos.Count, address, totalAmount);

                        if (totalAmount >= requiredAmount)
                        {
                            _logger.LogInformation("Required amount met for address {Address}. Required: {RequiredAmount}, Available: {TotalAmount}", 
                                address, requiredAmount, totalAmount);
                            return mempoolUtxos;
                        }
                        else
                        {
                            _logger.LogInformation("Partial funding detected for address {Address}. Required: {RequiredAmount}, Available: {TotalAmount}. Continuing to monitor...", 
                                address, requiredAmount, totalAmount);
                        }
                    }
                }

                // Wait before next poll
                await Task.Delay(_pollingInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while monitoring address {Address}: {Error}", address, ex.Message);
                
                // Continue monitoring despite errors, but wait before retry
                await Task.Delay(_pollingInterval, cancellationToken);
            }
        }

        // Timeout reached
        var elapsed = DateTime.UtcNow - startTime;
        _logger.LogWarning("Mempool monitoring timeout for address {Address} after {Elapsed}. Required amount: {RequiredAmount} sats not received", 
            address, elapsed, requiredAmount);
        
        throw new TimeoutException($"Timeout waiting for funds on address {address}. Required: {requiredAmount} sats");
    }
}

