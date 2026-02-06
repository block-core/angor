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
    /// </summary>
    /// <param name="swapId">The Boltz swap ID to monitor</param>
    /// <param name="timeout">Maximum time to wait (default 30 minutes)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Final swap status when complete or failed</returns>
    Task<Result<BoltzSwapStatus>> MonitorSwapAsync(
        string swapId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}

