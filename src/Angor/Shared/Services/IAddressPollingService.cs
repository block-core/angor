using Angor.Shared.Models;

namespace Angor.Shared.Services;

/// <summary>
/// Service for polling an address for incoming funds via the indexer.
/// This is the core polling logic extracted from MempoolMonitoringService
/// so it can be reused by both the Avalonia SDK and the Blazor client directly.
/// </summary>
public interface IAddressPollingService
{
    /// <summary>
    /// Polls an address for incoming funds until the required amount is detected, timeout occurs, or cancellation is requested.
    /// </summary>
    /// <param name="address">The Bitcoin address to monitor</param>
    /// <param name="requiredSats">The minimum amount in satoshis required</param>
    /// <param name="timeout">Maximum time to wait for funds</param>
    /// <param name="pollInterval">Time between each poll</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected mempool UTXOs that meet or exceed the required amount, or empty list on timeout/cancellation</returns>
    Task<List<UtxoData>> WaitForFundsAsync(
        string address,
        long requiredSats,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken);
}

