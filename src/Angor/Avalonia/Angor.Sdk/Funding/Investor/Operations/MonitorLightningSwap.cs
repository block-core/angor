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
/// Monitors a Boltz submarine swap until completion.
/// Polls Boltz API for status, then fetches UTXOs from indexer once complete.
/// </summary>
public static class MonitorLightningSwap
{
    /// <summary>
    /// Request to monitor a Lightning swap until funds arrive on-chain
    /// </summary>
    /// <param name="WalletId">The Angor wallet ID</param>
    /// <param name="SwapId">The Boltz swap ID to monitor</param>
    /// <param name="ReceivingAddress">The on-chain address expecting funds</param>
    /// <param name="Timeout">Maximum time to wait (default 30 minutes)</param>
    public record MonitorLightningSwapRequest(
        WalletId WalletId,
        string SwapId,
        string ReceivingAddress,
        TimeSpan? Timeout = null) : IRequest<Result<MonitorLightningSwapResponse>>;

    /// <summary>
    /// Response containing the completed swap details
    /// </summary>
    /// <param name="SwapStatus">Final swap status</param>
    /// <param name="TransactionId">On-chain transaction ID</param>
    /// <param name="DetectedUtxos">UTXOs detected on the receiving address</param>
    public record MonitorLightningSwapResponse(
        BoltzSwapStatus SwapStatus,
        string TransactionId,
        List<UtxoData> DetectedUtxos);

    public class MonitorLightningSwapHandler(
        IBoltzSwapService boltzSwapService,
        IIndexerService indexerService,
        IWalletAccountBalanceService walletAccountBalanceService,
        ILogger<MonitorLightningSwapHandler> logger)
        : IRequestHandler<MonitorLightningSwapRequest, Result<MonitorLightningSwapResponse>>
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

        public async Task<Result<MonitorLightningSwapResponse>> Handle(
            MonitorLightningSwapRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation(
                    "Monitoring Lightning swap {SwapId} for wallet {WalletId}",
                    request.SwapId, request.WalletId.Value);

                // Step 1: Poll Boltz until swap completes or fails
                var swapResult = await PollBoltzUntilComplete(
                    request.SwapId, 
                    request.Timeout ?? DefaultTimeout, 
                    cancellationToken);

                if (swapResult.IsFailure)
                {
                    return Result.Failure<MonitorLightningSwapResponse>(swapResult.Error);
                }

                var finalStatus = swapResult.Value;

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
                        finalStatus.TransactionId!,
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
                    finalStatus.TransactionId!,
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

        private async Task<Result<BoltzSwapStatus>> PollBoltzUntilComplete(
            string swapId,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow.Add(timeout);

            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                var statusResult = await boltzSwapService.GetSwapStatusAsync(swapId);

                if (statusResult.IsFailure)
                {
                    logger.LogWarning("Failed to get swap status: {Error}", statusResult.Error);
                    await Task.Delay(PollInterval, cancellationToken);
                    continue;
                }

                var status = statusResult.Value;
                logger.LogDebug("Swap {SwapId} status: {Status}", swapId, status.Status);

                // Swap completed successfully
                if (status.Status.IsComplete())
                {
                    logger.LogInformation(
                        "Swap {SwapId} completed! Transaction: {TxId}",
                        swapId, status.TransactionId);
                    return Result.Success(status);
                }

                // Swap failed
                if (status.Status.IsFailed())
                {
                    var errorMsg = $"Swap failed: {status.Status}";
                    if (!string.IsNullOrEmpty(status.FailureReason))
                        errorMsg += $" - {status.FailureReason}";
                    
                    logger.LogError("Swap {SwapId} failed: {Error}", swapId, errorMsg);
                    return Result.Failure<BoltzSwapStatus>(errorMsg);
                }

                // Transaction is on-chain (mempool or confirmed) - good enough to proceed
                if (status.Status == SwapState.TransactionMempool ||
                    status.Status == SwapState.TransactionConfirmed)
                {
                    logger.LogInformation(
                        "Swap {SwapId} has on-chain transaction: {TxId} (status: {Status})",
                        swapId, status.TransactionId, status.Status);
                    return Result.Success(status);
                }

                await Task.Delay(PollInterval, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Result.Failure<BoltzSwapStatus>("Monitoring was cancelled");
            }

            return Result.Failure<BoltzSwapStatus>(
                "Timeout waiting for Lightning payment. Please pay the invoice and try monitoring again.");
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

