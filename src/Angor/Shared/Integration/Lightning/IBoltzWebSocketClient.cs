using Angor.Shared.Integration.Lightning.Models;
using CSharpFunctionalExtensions;

namespace Angor.Shared.Integration.Lightning;

/// <summary>
/// WebSocket client for real-time Boltz swap status updates.
/// </summary>
public interface IBoltzWebSocketClient
{
    /// <summary>
    /// Monitors a swap via WebSocket until it reaches a terminal state.
    /// Returns when the swap completes, fails, or times out.
    /// </summary>
    Task<Result<BoltzSwapStatus>> MonitorSwapAsync(
        string swapId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}

