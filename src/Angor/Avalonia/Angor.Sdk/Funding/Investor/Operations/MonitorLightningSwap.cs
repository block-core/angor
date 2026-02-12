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
/// 3. When status is "transaction.mempool" or "transaction.claimed", the swap is complete
/// 4. Fetches UTXOs from the receiving address
/// 5. Returns UTXOs for use in the normal investment flow (BuildInvestmentDraft)
/// 
/// Note: For reverse submarine swaps, Boltz locks funds on-chain after the Lightning invoice is paid.
/// The funds are then claimed to the destination address using MuSig2 cooperative signing.
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
        TimeSpan? Timeout = null) : IRequest<Result<MonitorLightningSwapResponse>>;

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
        List<UtxoData> DetectedUtxos);

    public class MonitorLightningSwapHandler(
        IBoltzWebSocketClient webSocketClient,
        IBoltzSwapStorageService swapStorageService,
        IIndexerService indexerService,
        IWalletAccountBalanceService walletAccountBalanceService,
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
                    request.SwapId, request.WalletId.Value);

                // Step 1: Monitor swap via WebSocket until complete
                var swapResult = await webSocketClient.MonitorSwapAsync(
                    request.SwapId,
                    timeout: request.Timeout ?? DefaultTimeout,
                    cancellationToken: cancellationToken);

                if (swapResult.IsFailure)
                {
                    return Result.Failure<MonitorLightningSwapResponse>(swapResult.Error);
                }

                var finalStatus = swapResult.Value;

                // Step 1.5: Update swap status in database
                await swapStorageService.UpdateSwapStatusAsync(
                    request.SwapId,
                    finalStatus.Status.ToString(),
                    finalStatus.TransactionId,
                    lockupTxHex: null); // We don't have the hex here, would need to fetch from indexer

                // Step 2: Fetch UTXOs from indexer (single call, no polling needed)
                var utxos = await FetchUtxosFromIndexer(request.ReceivingAddress);

                if (utxos == null || !utxos.Any())
                {
                    logger.LogWarning(
                        "Swap completed but no UTXOs found yet on address {Address}. Transaction: {TxId}",
                        request.ReceivingAddress, finalStatus.TransactionId);
                    
                    // Return success anyway - tx exists, UTXOs may need a moment to appear
                    return Result.Success(new MonitorLightningSwapResponse(
                        finalStatus,
                        finalStatus.TransactionId ?? string.Empty,
                        new List<UtxoData>()));
                }

                var totalAmount = utxos.Sum(u => u.value);
                logger.LogInformation(
                    "Swap {SwapId} complete. Detected {Count} UTXO(s) totaling {Amount} sats",
                    request.SwapId, utxos.Count, totalAmount);

                // Step 3: Update wallet balance
                await UpdateWalletBalance(request.WalletId, request.ReceivingAddress, utxos);

                return Result.Success(new MonitorLightningSwapResponse(
                    finalStatus,
                    finalStatus.TransactionId ?? string.Empty,
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


        private async Task<List<UtxoData>?> FetchUtxosFromIndexer(string address)
        {
            try
            {
                logger.LogDebug("Fetching UTXOs for address {Address} from indexer", address);
                
                var utxos = await indexerService.FetchUtxoAsync(address, limit: 100, offset: 0);
                
                if (utxos != null && utxos.Any())
                {
                    logger.LogDebug("Found {Count} UTXO(s) on address {Address}", utxos.Count, address);
                }
                
                return utxos;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch UTXOs for address {Address}", address);
                return null;
            }
        }

        private async Task UpdateWalletBalance(WalletId walletId, string address, List<UtxoData> utxos)
        {
            try
            {
                var accountResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(walletId);
                if (accountResult.IsSuccess)
                {
                    var accountInfo = accountResult.Value.AccountInfo;
                    var addedCount = accountInfo.AddNewUtxos(address, utxos);

                    if (addedCount > 0)
                    {
                        await walletAccountBalanceService.SaveAccountBalanceInfoAsync(
                            walletId, accountResult.Value);
                        logger.LogDebug("Added {Count} UTXOs to wallet {WalletId}", addedCount, walletId.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to update wallet balance - UTXOs still valid");
            }
        }
    }
}

