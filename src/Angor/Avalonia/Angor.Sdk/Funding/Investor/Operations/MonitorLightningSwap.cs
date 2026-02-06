using Angor.Sdk.Common;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Monitors a Boltz submarine swap until completion.
/// Polls Boltz API for status and monitors on-chain for the transaction.
/// </summary>
public static class MonitorLightningSwap
{
    /// <summary>
    /// Request to monitor a Lightning swap until funds arrive on-chain
    /// </summary>
    /// <param name="WalletId">The Angor wallet ID</param>
    /// <param name="SwapId">The Boltz swap ID to monitor</param>
    /// <param name="ReceivingAddress">The on-chain address expecting funds</param>
    /// <param name="ExpectedAmount">Expected amount in satoshis</param>
    /// <param name="Timeout">Maximum time to wait (default 30 minutes)</param>
    public record MonitorLightningSwapRequest(
        WalletId WalletId,
        string SwapId,
        string ReceivingAddress,
        long ExpectedAmount,
        TimeSpan? Timeout = null) : IRequest<Result<MonitorLightningSwapResponse>>;

    /// <summary>
    /// Response containing the completed swap details
    /// </summary>
    /// <param name="SwapStatus">Final swap status</param>
    /// <param name="TransactionId">On-chain transaction ID</param>
    /// <param name="DetectedUtxos">UTXOs detected on the receiving address</param>
    public record MonitorLightningSwapResponse(
        BoltzSwapStatus SwapStatus,
        string? TransactionId,
        List<UtxoData>? DetectedUtxos);

    public class MonitorLightningSwapHandler(
        IBoltzSwapService boltzSwapService,
        IMempoolMonitoringService mempoolMonitoringService,
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

                var timeout = request.Timeout ?? DefaultTimeout;
                var deadline = DateTime.UtcNow.Add(timeout);

                BoltzSwapStatus? finalStatus = null;

                // Poll Boltz for swap status
                while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
                {
                    var statusResult = await boltzSwapService.GetSwapStatusAsync(request.SwapId);
                    
                    if (statusResult.IsFailure)
                    {
                        logger.LogWarning("Failed to get swap status: {Error}", statusResult.Error);
                        await Task.Delay(PollInterval, cancellationToken);
                        continue;
                    }

                    var status = statusResult.Value;
                    logger.LogDebug("Swap {SwapId} status: {Status}", request.SwapId, status.Status);

                    // Check if swap is complete
                    if (status.Status.IsComplete())
                    {
                        logger.LogInformation(
                            "Swap {SwapId} completed! Transaction: {TxId}",
                            request.SwapId, status.TransactionId);
                        finalStatus = status;
                        break;
                    }

                    // Check if swap failed
                    if (status.Status.IsFailed())
                    {
                        logger.LogError(
                            "Swap {SwapId} failed: {Status} - {Reason}",
                            request.SwapId, status.Status, status.FailureReason);
                        return Result.Failure<MonitorLightningSwapResponse>(
                            $"Swap failed: {status.Status} - {status.FailureReason ?? "Unknown reason"}");
                    }

                    // Check if we have a transaction (even if not fully confirmed)
                    if (status.Status == SwapState.TransactionMempool || 
                        status.Status == SwapState.TransactionConfirmed)
                    {
                        logger.LogInformation(
                            "Swap {SwapId} has on-chain transaction: {TxId} (status: {Status})",
                            request.SwapId, status.TransactionId, status.Status);
                        finalStatus = status;
                        break;
                    }

                    await Task.Delay(PollInterval, cancellationToken);
                }

                if (finalStatus == null)
                {
                    return Result.Failure<MonitorLightningSwapResponse>(
                        "Timeout waiting for Lightning payment. Please pay the invoice and try monitoring again.");
                }

                // Now monitor the on-chain address for UTXOs
                List<UtxoData>? detectedUtxos = null;
                
                if (!string.IsNullOrEmpty(finalStatus.TransactionId))
                {
                    try
                    {
                        logger.LogInformation(
                            "Monitoring address {Address} for swap funds",
                            request.ReceivingAddress);

                        // Give it a shorter timeout since we know the tx exists
                        var utxos = await mempoolMonitoringService.MonitorAddressForFundsAsync(
                            request.ReceivingAddress,
                            request.ExpectedAmount,
                            TimeSpan.FromMinutes(5),
                            cancellationToken);

                        if (utxos.Any())
                        {
                            detectedUtxos = utxos;
                            var totalAmount = utxos.Sum(u => u.value);
                            
                            logger.LogInformation(
                                "Detected {Count} UTXO(s) totaling {Amount} sats on address {Address}",
                                utxos.Count, totalAmount, request.ReceivingAddress);

                            // Update wallet balance
                            await UpdateWalletBalance(request.WalletId, request.ReceivingAddress, utxos);
                        }
                    }
                    catch (TimeoutException)
                    {
                        logger.LogWarning(
                            "Timeout detecting UTXOs, but swap transaction exists: {TxId}",
                            finalStatus.TransactionId);
                    }
                }

                return Result.Success(new MonitorLightningSwapResponse(
                    finalStatus,
                    finalStatus.TransactionId,
                    detectedUtxos));
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

