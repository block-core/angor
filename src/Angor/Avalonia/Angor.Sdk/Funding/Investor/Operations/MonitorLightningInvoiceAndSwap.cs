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
/// Monitors a Lightning invoice for payment and initiates on-chain swap when paid.
/// This connects Lightning payments to the existing investment transaction flow.
/// </summary>
public static class MonitorLightningInvoiceAndSwap
{
    /// <summary>
    /// Request to monitor a Lightning invoice and swap to on-chain when paid
    /// </summary>
    /// <param name="WalletId">The Angor wallet ID</param>
    /// <param name="InvoiceId">The Lightning invoice ID to monitor</param>
    /// <param name="BoltWalletId">The Bolt wallet ID</param>
    /// <param name="TargetAddress">The on-chain Bitcoin address to send funds to after swap</param>
    /// <param name="Timeout">Maximum time to wait for payment (default 30 minutes)</param>
    public record MonitorLightningInvoiceRequest(
        WalletId WalletId,
        string InvoiceId,
        string BoltWalletId,
        string TargetAddress,
        TimeSpan? Timeout = null) : IRequest<Result<MonitorLightningInvoiceResponse>>;

    /// <summary>
    /// Response containing the swap transaction details
    /// </summary>
    /// <param name="Invoice">The paid invoice</param>
    /// <param name="SwapAddress">The on-chain address used for the swap</param>
    /// <param name="DetectedUtxos">UTXOs detected after swap (if completed)</param>
    public record MonitorLightningInvoiceResponse(
        BoltInvoice Invoice,
        string SwapAddress,
        List<UtxoData>? DetectedUtxos = null);

    public class MonitorLightningInvoiceHandler(
        IBoltService boltService,
        IMempoolMonitoringService mempoolMonitoringService,
        IWalletAccountBalanceService walletAccountBalanceService,
        ILogger<MonitorLightningInvoiceHandler> logger)
        : IRequestHandler<MonitorLightningInvoiceRequest, Result<MonitorLightningInvoiceResponse>>
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        public async Task<Result<MonitorLightningInvoiceResponse>> Handle(
            MonitorLightningInvoiceRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation(
                    "Monitoring Lightning invoice {InvoiceId} for payment",
                    request.InvoiceId);

                var timeout = request.Timeout ?? DefaultTimeout;
                var deadline = DateTime.UtcNow.Add(timeout);

                // Poll for invoice payment
                BoltInvoice? paidInvoice = null;
                while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
                {
                    var invoiceResult = await boltService.GetInvoiceAsync(request.InvoiceId);
                    if (invoiceResult.IsFailure)
                    {
                        logger.LogWarning("Failed to get invoice status: {Error}", invoiceResult.Error);
                        await Task.Delay(PollInterval, cancellationToken);
                        continue;
                    }

                    var invoice = invoiceResult.Value;

                    if (invoice.Status == BoltPaymentStatus.Paid)
                    {
                        logger.LogInformation(
                            "Lightning invoice {InvoiceId} paid! Amount: {Amount} sats",
                            request.InvoiceId, invoice.AmountSats);
                        paidInvoice = invoice;
                        break;
                    }

                    if (invoice.Status == BoltPaymentStatus.Expired || invoice.Status == BoltPaymentStatus.Failed)
                    {
                        return Result.Failure<MonitorLightningInvoiceResponse>(
                            $"Invoice {invoice.Status.ToString().ToLower()}");
                    }

                    await Task.Delay(PollInterval, cancellationToken);
                }

                if (paidInvoice == null)
                {
                    return Result.Failure<MonitorLightningInvoiceResponse>(
                        "Timeout waiting for Lightning invoice payment");
                }

                // Get swap address for converting Lightning to on-chain
                logger.LogInformation(
                    "Getting swap address to convert {Amount} sats to on-chain",
                    paidInvoice.AmountSats);

                var swapAddressResult = await boltService.GetSwapAddressAsync(
                    request.BoltWalletId,
                    paidInvoice.AmountSats);

                if (swapAddressResult.IsFailure)
                {
                    return Result.Failure<MonitorLightningInvoiceResponse>(swapAddressResult.Error);
                }

                var swapAddress = swapAddressResult.Value;
                logger.LogInformation("Swap address obtained: {Address}", swapAddress);

                // Monitor the swap address for the on-chain transaction
                // This will detect when the Lightning->On-chain swap completes
                logger.LogInformation(
                    "Monitoring swap address {Address} for on-chain confirmation",
                    swapAddress);

                var monitorResult = await MonitorSwapCompletion(
                    swapAddress,
                    paidInvoice.AmountSats,
                    request.WalletId,
                    request.TargetAddress,
                    cancellationToken);

                if (monitorResult.IsFailure)
                {
                    logger.LogWarning(
                        "Swap monitoring failed: {Error}. Funds may still arrive later.",
                        monitorResult.Error);
                }

                return Result.Success(new MonitorLightningInvoiceResponse(
                    paidInvoice,
                    swapAddress,
                    monitorResult.IsSuccess ? monitorResult.Value : null));
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Lightning invoice monitoring cancelled");
                return Result.Failure<MonitorLightningInvoiceResponse>("Monitoring was cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error monitoring Lightning invoice {InvoiceId}", request.InvoiceId);
                return Result.Failure<MonitorLightningInvoiceResponse>($"Error monitoring invoice: {ex.Message}");
            }
        }

        private async Task<Result<List<UtxoData>>> MonitorSwapCompletion(
            string swapAddress,
            long expectedAmount,
            WalletId walletId,
            string targetAddress,
            CancellationToken cancellationToken)
        {
            try
            {
                // Monitor the swap address for incoming on-chain funds
                // Allow 15 minutes for the swap to complete
                var swapTimeout = TimeSpan.FromMinutes(15);
                
                var utxos = await mempoolMonitoringService.MonitorAddressForFundsAsync(
                    swapAddress,
                    expectedAmount,
                    swapTimeout,
                    cancellationToken);

                if (!utxos.Any())
                {
                    return Result.Failure<List<UtxoData>>("No UTXOs detected after swap timeout");
                }

                logger.LogInformation(
                    "Swap completed! Detected {Count} UTXO(s) on address {Address}, total: {Amount} sats",
                    utxos.Count, swapAddress, utxos.Sum(u => u.value));

                // Update wallet balance with the new UTXOs on the target address
                var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(walletId);
                if (accountBalanceResult.IsSuccess)
                {
                    var accountInfo = accountBalanceResult.Value.AccountInfo;
                    accountInfo.AddNewUtxos(targetAddress, utxos);
                    await walletAccountBalanceService.SaveAccountBalanceInfoAsync(walletId, accountBalanceResult.Value);
                }

                return Result.Success(utxos);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error monitoring swap completion");
                return Result.Failure<List<UtxoData>>($"Error monitoring swap: {ex.Message}");
            }
        }
    }
}

