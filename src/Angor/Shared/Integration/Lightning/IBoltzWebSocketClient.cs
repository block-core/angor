<<<<<<<< HEAD:src/Angor/Shared/Integration/Lightning/IBoltzWebSocketClient.cs
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
========
// Moved to Angor.Shared.Integration.Lightning — see LightningGlobalUsings.cs
>>>>>>>> 2f47cc51 (Refactor Boltz integration: move models and services to Angor.Shared.Integration.Lightning namespace):src/Angor/Avalonia/Angor.Sdk/Integration/Lightning/IBoltzWebSocketClient.cs

