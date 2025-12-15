using Angor.Shared.Models;

namespace Angor.Shared.Services;

public interface IMempoolMonitoringService
{
    /// <summary>
    /// Monitors a specific address for incoming funds in the mempool until the required amount is detected or timeout occurs.
    /// </summary>
    /// <param name="address">The Bitcoin address to monitor</param>
    /// <param name="requiredAmount">The minimum amount in satoshis required</param>
    /// <param name="timeout">Maximum time to wait for funds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected UTXOs that meet or exceed the required amount</returns>
    Task<List<UtxoData>> MonitorAddressForFundsAsync(
        string address, 
        long requiredAmount, 
        TimeSpan timeout, 
        CancellationToken cancellationToken);
}

