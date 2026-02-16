using Angor.Sdk.Common;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Monitors a Boltz reverse submarine swap until funds are claimed.
/// 
/// Flow:
/// 1. Connects to Boltz WebSocket and subscribes to swap updates
/// 2. Receives real-time status updates (no polling!)
/// 3. When status is "transaction.mempool" or "transaction.confirmed", funds are locked
/// 4. Claims the funds using MuSig2 cooperative signing with our preimage
/// 5. Returns the claim transaction ID - the ViewModel can then use existing
///    address monitoring to track when the funds arrive
/// 
/// Note: For reverse submarine swaps, Boltz locks funds on-chain after the Lightning invoice is paid.
/// Only WE have the preimage, so we must claim manually (Boltz cannot auto-claim for us).
/// </summary>
public static class MonitorLightningSwap
{
    /// <summary>
    /// Request to monitor a Lightning swap until funds are claimed
    /// </summary>
    /// <param name="WalletId">The Angor wallet ID</param>
    /// <param name="SwapId">The Boltz swap ID to monitor</param>
    /// <param name="Timeout">Maximum time to wait (default 30 minutes)</param>
    public record MonitorLightningSwapRequest(
        WalletId WalletId,
        string SwapId,
        TimeSpan? Timeout = null
    ) : IRequest<Result<MonitorLightningSwapResponse>>;

    /// <summary>
    /// Response containing the completed swap details.
    /// After receiving this response, use existing address monitoring in the ViewModel
    /// to detect when the claimed funds arrive at the destination address.
    /// </summary>
    /// <param name="SwapStatus">Final swap status</param>
    /// <param name="ClaimTransactionId">The claim transaction ID (funds sent to destination address)</param>
    public record MonitorLightningSwapResponse(
        BoltzSwapStatus SwapStatus,
        string ClaimTransactionId);

    public class MonitorLightningSwapHandler(
        IBoltzWebSocketClient webSocketClient,
        IBoltzSwapStorageService swapStorageService,
        IMediator mediator,
        ILogger<MonitorLightningSwapHandler> logger)
        : IRequestHandler<MonitorLightningSwapRequest, Result<MonitorLightningSwapResponse>>
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

        public async Task<Result<MonitorLightningSwapResponse>> Handle(
            MonitorLightningSwapRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation(
                    "Monitoring Lightning swap {SwapId} via WebSocket for wallet {WalletId}",
                    request.SwapId,
                    request.WalletId.Value);

                // Step 1: Monitor swap via WebSocket until funds are locked or claimed
                var swapResult = await webSocketClient.MonitorSwapAsync(
                    request.SwapId,
                    timeout: request.Timeout ?? DefaultTimeout,
                    cancellationToken: cancellationToken);

                if (swapResult.IsFailure)
                {
                    return Result.Failure<MonitorLightningSwapResponse>(swapResult.Error);
                }

                var swapStatus = swapResult.Value;
                string? claimTxId = swapStatus.TransactionId;

                // Step 2: Update swap status in database
                await swapStorageService.UpdateSwapStatusAsync(
                    request.SwapId,
                    request.WalletId.Value,
                    swapStatus.Status.ToString(),
                    swapStatus.TransactionId,
                    lockupTxHex: swapStatus.TransactionHex);

                // Step 3: Check if already claimed
                if (swapStatus.Status == SwapState.TransactionClaimed)
                {
                    logger.LogInformation(
                        "Swap {SwapId} already claimed. TxId: {TxId}",
                        request.SwapId,
                        claimTxId);

                    return Result.Success(new MonitorLightningSwapResponse(
                        swapStatus,
                        claimTxId ?? string.Empty));
                }

                // Step 4: If funds are locked but not claimed, we need to claim
                // For reverse submarine swaps, only WE have the preimage, so we must claim manually
                if (swapStatus.Status == SwapState.TransactionMempool ||
                    swapStatus.Status == SwapState.TransactionConfirmed)
                {
                    logger.LogInformation(
                        "Swap {SwapId} has funds locked (status: {Status}). Claiming with preimage...",
                        request.SwapId,
                        swapStatus.Status);

                    var claimResult = await ClaimSwapFundsAsync(
                        request.WalletId,
                        request.SwapId,
                        swapStatus.TransactionHex,
                        cancellationToken);

                    if (claimResult.IsFailure)
                    {
                        logger.LogError("Failed to claim swap {SwapId}: {Error}", request.SwapId, claimResult.Error);
                        return Result.Failure<MonitorLightningSwapResponse>(
                            $"Funds locked but claim failed: {claimResult.Error}");
                    }

                    claimTxId = claimResult.Value;
                    logger.LogInformation(
                        "Swap {SwapId} claimed successfully. Claim TX: {TxId}",
                        request.SwapId,
                        claimTxId);

                    // Update status to claimed in database
                    await swapStorageService.MarkSwapClaimedAsync(
                        request.SwapId,
                        request.WalletId.Value,
                        claimTxId);

                    swapStatus.Status = SwapState.TransactionClaimed;
                    swapStatus.TransactionId = claimTxId;

                    return Result.Success(new MonitorLightningSwapResponse(
                        swapStatus,
                        claimTxId));
                }

                // Unexpected status - return what we have
                logger.LogWarning(
                    "Swap {SwapId} ended with unexpected status: {Status}",
                    request.SwapId,
                    swapStatus.Status);

                return Result.Success(new MonitorLightningSwapResponse(
                    swapStatus,
                    claimTxId ?? string.Empty));
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Swap monitoring cancelled for {SwapId}", request.SwapId);
                return Result.Failure<MonitorLightningSwapResponse>("Monitoring was cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error monitoring swap {SwapId}", request.SwapId);
                return Result.Failure<MonitorLightningSwapResponse>($"Error monitoring swap: {ex.Message}");
            }
        }

        private async Task<Result<string>> ClaimSwapFundsAsync(
            WalletId walletId,
            string swapId,
            string? lockupTxHex,
            CancellationToken cancellationToken
        )
        {
            try
            {
                var claimRequest = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
                    walletId,
                    swapId,
                    LockupTransactionHex: lockupTxHex,
                    LockupOutputIndex: 0,
                    FeeRate: 2);

                var result = await mediator.Send(claimRequest, cancellationToken);

                if (result.IsFailure)
                {
                    return Result.Failure<string>(result.Error);
                }

                return Result.Success(result.Value.ClaimTransactionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error claiming swap {SwapId}", swapId);
                return Result.Failure<string>($"Claim error: {ex.Message}");
            }
        }
    }
}
