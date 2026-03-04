using Angor.Sdk.Integration.Lightning.Models;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Integration.Lightning;

/// <summary>
/// WebSocket client for real-time Boltz swap status updates.
/// </summary>
public interface IBoltzWebSocketClient
{
    /// <summary>
    /// Monitors a swap via WebSocket until it reaches a terminal state.
    /// Returns when the swap completes, fails, or times out.
    /// For reverse swaps, returns at TransactionMempool/TransactionConfirmed.
    /// </summary>
    /// <param name="swapId">The Boltz swap ID to monitor</param>
    /// <param name="timeout">Maximum time to wait (default 30 minutes)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Final swap status when complete or failed</returns>
    Task<Result<BoltzSwapStatus>> MonitorSwapAsync(
        string swapId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Monitors a chain swap via WebSocket until Boltz locks BTC (server lockup).
    /// Returns when TransactionServerMempool/TransactionServerConfirmed is reached,
    /// or on any terminal state (complete/failed).
    /// </summary>
    /// <param name="swapId">The Boltz chain swap ID to monitor</param>
    /// <param name="timeout">Maximum time to wait (default 30 minutes)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Swap status when server lockup is detected or terminal state reached</returns>
    Task<Result<BoltzSwapStatus>> MonitorChainSwapAsync(
        string swapId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}

