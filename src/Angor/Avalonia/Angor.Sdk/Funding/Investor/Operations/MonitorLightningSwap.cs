using Angor.Sdk.Common;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Monitors a Boltz reverse submarine swap until completion using WebSocket.
/// 
/// Flow:
/// 1. Connects to Boltz WebSocket and subscribes to swap updates
/// 2. Receives real-time status updates (no polling!)
/// 3. When status is "transaction.mempool" or "transaction.confirmed", funds are locked
/// 4. If not automatically claimed, triggers manual claim via ClaimLightningSwap
/// 5. Fetches UTXOs from the receiving address
/// 6. Returns UTXOs for use in the normal investment flow (BuildInvestmentDraft)
/// 
/// Note: For reverse submarine swaps, Boltz locks funds on-chain after the Lightning invoice is paid.
/// The funds are then claimed to the destination address using MuSig2 cooperative signing.
/// If an address was provided when creating the swap, Boltz performs automatic claiming.
/// Otherwise, we must claim manually using the preimage and our claim key.
/// </summary>
public static class MonitorLightningSwap
{
    /// <summary>
    /// Request to monitor a Lightning swap until funds arrive on-chain
    /// </summary>
    /// <param name="WalletId">The Angor wallet ID</param>
    /// <param name="SwapId">The Boltz swap ID to monitor</param>
    /// <param name="ReceivingAddress">The on-chain address expecting funds (destination address from swap creation)</param>
    /// <param name="Timeout">Maximum time to wait (default 30 minutes)</param>
    public record MonitorLightningSwapRequest(
        WalletId WalletId,
        string SwapId,
        string ReceivingAddress,
        TimeSpan? Timeout = null
    ) : IRequest<Result<MonitorLightningSwapResponse>>;

    /// <summary>
    /// Response containing the completed swap details.
    /// Use the DetectedUtxos with BuildInvestmentDraft.BuildInvestmentDraftRequest.FundingAddress
    /// to create the investment transaction from these specific UTXOs.
    /// </summary>
    /// <param name="SwapStatus">Final swap status</param>
    /// <param name="TransactionId">On-chain transaction ID (claim transaction)</param>
    /// <param name="DetectedUtxos">UTXOs detected on the receiving address, ready for investment</param>
    public record MonitorLightningSwapResponse(
        BoltzSwapStatus SwapStatus,
        string TransactionId,
        List<UtxoData> DetectedUtxos
    );

    public class MonitorLightningSwapHandler(
        IBoltzWebSocketClient webSocketClient,
        IBoltzSwapStorageService swapStorageService,
        IIndexerService indexerService,
        IWalletAccountBalanceService walletAccountBalanceService,
        IMediator mediator,
        ILogger<MonitorLightningSwapHandler> logger
    )
        : IRequestHandler<MonitorLightningSwapRequest, Result<MonitorLightningSwapResponse>>
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan UtxoPollingInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan UtxoPollingTimeout = TimeSpan.FromMinutes(2);

        public async Task<Result<MonitorLightningSwapResponse>> Handle(
            MonitorLightningSwapRequest request,
            CancellationToken cancellationToken
        )
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
                }
                // Step 4: If funds are locked but not claimed, we need to claim
                // For reverse submarine swaps, only WE have the preimage, so we must claim manually
                else if (swapStatus.Status == SwapState.TransactionMempool ||
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

                    swapStatus.Status = SwapState.TransactionClaimed;
                    swapStatus.TransactionId = claimTxId;
                }

                // Step 4: Fetch UTXOs from indexer with polling (claim tx may need time to propagate)
                var utxos = await FetchUtxosWithPollingAsync(request.ReceivingAddress, cancellationToken);

                if (utxos == null || !utxos.Any())
                {
                    logger.LogWarning(
                        "Swap claimed but no UTXOs found on address {Address}. Claim TX: {TxId}",
                        request.ReceivingAddress,
                        claimTxId);

                    // Return success anyway - claim tx was broadcast
                    return Result.Success(
                        new MonitorLightningSwapResponse(
                            swapStatus,
                            claimTxId ?? string.Empty,
                            new List<UtxoData>()));
                }

                var totalAmount = utxos.Sum(u => u.value);
                logger.LogInformation(
                    "Swap {SwapId} complete. Detected {Count} UTXO(s) totaling {Amount} sats on {Address}",
                    request.SwapId,
                    utxos.Count,
                    totalAmount,
                    request.ReceivingAddress);

                // Step 5: Update wallet balance
                await UpdateWalletBalance(request.WalletId, utxos);

                return Result.Success(
                    new MonitorLightningSwapResponse(
                        swapStatus,
                        claimTxId ?? string.Empty,
                        utxos));
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

        private async Task<List<UtxoData>?> FetchUtxosWithPollingAsync(
            string address,
            CancellationToken cancellationToken
        )
        {
            var startTime = DateTime.UtcNow;
            var endTime = startTime.Add(UtxoPollingTimeout);

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                var utxos = await FetchUtxosFromIndexer(address);

                if (utxos != null && utxos.Any())
                {
                    return utxos;
                }

                logger.LogDebug(
                    "No UTXOs found yet on {Address}, polling again in {Interval}s...",
                    address,
                    UtxoPollingInterval.TotalSeconds);

                await Task.Delay(UtxoPollingInterval, cancellationToken);
            }

            // Final attempt
            return await FetchUtxosFromIndexer(address);
        }

        private async Task<List<UtxoData>?> FetchUtxosFromIndexer(string address)
        {
            try
            {
                logger.LogDebug("Fetching UTXOs for address {Address} from indexer", address);
                
                var utxos = await indexerService.FetchUtxoAsync(address, 0, 1);
                
                if (utxos == null || !utxos.Any())
                {
                    return null;
                }

                return utxos.ToList();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error fetching UTXOs for address {Address}", address);
                return null;
            }
        }

        private async Task UpdateWalletBalance(WalletId walletId, List<UtxoData> utxos)
        {
            try
            {
                // Update the wallet balance with the new UTXOs
                var totalValue = utxos.Sum(u => u.value);
                logger.LogDebug(
                    "Updating wallet {WalletId} balance with {Count} UTXO(s) totaling {Amount} sats",
                    walletId.Value, utxos.Count, totalValue);

                await walletAccountBalanceService.RefreshAccountBalanceInfoAsync(walletId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error updating wallet balance for {WalletId}", walletId.Value);
                // Don't fail the operation if balance update fails
            }
        }
    }
}
