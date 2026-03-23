using Angor.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services;

/// <summary>
/// Polls an address for incoming funds via the indexer.
/// Core polling logic extracted from MempoolMonitoringService so it can be reused
/// by both the Avalonia SDK (via MediatR handlers) and the Blazor client directly.
/// 
/// Unlike MempoolMonitoringService, this service returns empty list on timeout/cancellation
/// instead of throwing, making it suitable for direct UI consumption.
/// </summary>
public class AddressPollingService : IAddressPollingService
{
    private readonly IIndexerService _indexerService;
    private readonly ILogger<AddressPollingService> _logger;

    public AddressPollingService(
        IIndexerService indexerService,
        ILogger<AddressPollingService> logger)
    {
        _indexerService = indexerService;
        _logger = logger;
    }

    public async Task<List<UtxoData>> WaitForFundsAsync(
        string address,
        long requiredSats,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting address polling for {Address}, required: {RequiredSats} sats, timeout: {Timeout}",
            address, requiredSats, timeout);

        var endTime = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < endTime)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Address polling cancelled for {Address}", address);
                return new List<UtxoData>();
            }

            try
            {
                var utxos = await _indexerService.FetchUtxoAsync(address, limit: 50, offset: 0);

                if (utxos != null && utxos.Any())
                {
                    // Filter for unconfirmed transactions (mempool) - blockIndex == 0 means unconfirmed
                    var mempoolUtxos = utxos
                        .Where(u => u.blockIndex == 0)
                        .ToList();

                    if (mempoolUtxos.Any())
                    {
                        var totalAmount = mempoolUtxos.Sum(u => u.value);

                        _logger.LogInformation(
                            "Detected {Count} mempool UTXO(s) for {Address}, total: {TotalAmount} sats",
                            mempoolUtxos.Count, address, totalAmount);

                        if (totalAmount >= requiredSats)
                        {
                            _logger.LogInformation(
                                "Required amount met for {Address}. Required: {RequiredSats}, Available: {TotalAmount}",
                                address, requiredSats, totalAmount);
                            return mempoolUtxos;
                        }

                        _logger.LogInformation(
                            "Partial funding for {Address}. Required: {RequiredSats}, Available: {TotalAmount}. Continuing...",
                            address, requiredSats, totalAmount);
                    }
                }

                await Task.Delay(pollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Address polling cancelled for {Address}", address);
                return new List<UtxoData>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error polling address {Address}: {Error}", address, ex.Message);
                
                try
                {
                    await Task.Delay(pollInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return new List<UtxoData>();
                }
            }
        }

        _logger.LogWarning(
            "Timeout polling address {Address} after {Timeout}. Required: {RequiredSats} sats not received",
            address, timeout, requiredSats);

        return new List<UtxoData>();
    }
}

