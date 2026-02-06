using Angor.Sdk.Common;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Orchestrates the complete flow from Lightning payment to investment transaction.
/// 1. Creates a Lightning invoice
/// 2. Monitors for payment
/// 3. Swaps Lightning funds to on-chain
/// 4. Uses the swapped funds for the investment transaction
/// </summary>
public static class FundInvestmentViaLightning
{
    /// <summary>
    /// Request to fund an investment via Lightning Network
    /// </summary>
    /// <param name="WalletId">The Angor wallet ID</param>
    /// <param name="ProjectId">The project to invest in</param>
    /// <param name="InvestmentAmount">Amount to invest in satoshis</param>
    /// <param name="ReceivingAddress">The on-chain address in the Angor wallet to receive swapped funds</param>
    /// <param name="InvoiceTimeout">Maximum time to wait for invoice payment (default 30 minutes)</param>
    public record FundInvestmentViaLightningRequest(
        WalletId WalletId,
        string ProjectId,
        Amount InvestmentAmount,
        string ReceivingAddress,
        TimeSpan? InvoiceTimeout = null) : IRequest<Result<FundInvestmentViaLightningResponse>>;

    /// <summary>
    /// Response containing the complete flow results
    /// </summary>
    /// <param name="Invoice">The created and paid Lightning invoice</param>
    /// <param name="BoltWalletId">The Bolt wallet ID used</param>
    /// <param name="SwapAddress">The address used for Lightning to on-chain swap</param>
    /// <param name="DetectedUtxos">UTXOs received after swap</param>
    /// <param name="TotalAmountReceived">Total amount received on-chain in satoshis</param>
    public record FundInvestmentViaLightningResponse(
        BoltInvoice Invoice,
        string BoltWalletId,
        string SwapAddress,
        List<UtxoData> DetectedUtxos,
        Amount TotalAmountReceived);

    public class FundInvestmentViaLightningHandler(
        IMediator mediator,
        ILogger<FundInvestmentViaLightningHandler> logger)
        : IRequestHandler<FundInvestmentViaLightningRequest, Result<FundInvestmentViaLightningResponse>>
    {
        public async Task<Result<FundInvestmentViaLightningResponse>> Handle(
            FundInvestmentViaLightningRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation(
                    "Starting Lightning funding flow for wallet {WalletId}, project {ProjectId}, amount: {Amount} sats",
                    request.WalletId.Value, request.ProjectId, request.InvestmentAmount.Sats);

                // Step 1: Create Lightning invoice
                logger.LogInformation("Step 1: Creating Lightning invoice");
                var createInvoiceRequest = new CreateLightningInvoiceForInvestment.CreateLightningInvoiceRequest(
                    request.WalletId,
                    request.ProjectId,
                    request.InvestmentAmount);

                var invoiceResult = await mediator.Send(createInvoiceRequest, cancellationToken);
                if (invoiceResult.IsFailure)
                {
                    logger.LogError("Failed to create Lightning invoice: {Error}", invoiceResult.Error);
                    return Result.Failure<FundInvestmentViaLightningResponse>(
                        $"Failed to create invoice: {invoiceResult.Error}");
                }

                var invoice = invoiceResult.Value.Invoice;
                var boltWalletId = invoiceResult.Value.BoltWalletId;

                logger.LogInformation(
                    "Lightning invoice created successfully. Bolt11: {Bolt11}",
                    invoice.Bolt11);

                // Step 2: Monitor invoice and swap to on-chain
                logger.LogInformation("Step 2: Monitoring invoice for payment and initiating swap");
                var monitorRequest = new MonitorLightningInvoiceAndSwap.MonitorLightningInvoiceRequest(
                    request.WalletId,
                    invoice.Id,
                    boltWalletId,
                    request.ReceivingAddress,
                    request.InvoiceTimeout);

                var monitorResult = await mediator.Send(monitorRequest, cancellationToken);
                if (monitorResult.IsFailure)
                {
                    logger.LogError("Failed to monitor invoice or swap: {Error}", monitorResult.Error);
                    return Result.Failure<FundInvestmentViaLightningResponse>(
                        $"Failed to process payment: {monitorResult.Error}");
                }

                var paidInvoice = monitorResult.Value.Invoice;
                var swapAddress = monitorResult.Value.SwapAddress;
                var detectedUtxos = monitorResult.Value.DetectedUtxos ?? new List<UtxoData>();

                if (!detectedUtxos.Any())
                {
                    logger.LogWarning("Invoice was paid but no UTXOs detected yet. Funds may arrive later.");
                    return Result.Failure<FundInvestmentViaLightningResponse>(
                        "Invoice paid but on-chain swap not completed yet. Please try again later.");
                }

                var totalReceived = detectedUtxos.Sum(u => u.value);

                logger.LogInformation(
                    "Lightning funding flow completed successfully! Received {Amount} sats on-chain",
                    totalReceived);

                return Result.Success(new FundInvestmentViaLightningResponse(
                    paidInvoice,
                    boltWalletId,
                    swapAddress,
                    detectedUtxos,
                    new Amount(totalReceived)));
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Lightning funding flow cancelled");
                return Result.Failure<FundInvestmentViaLightningResponse>("Operation was cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Lightning funding flow");
                return Result.Failure<FundInvestmentViaLightningResponse>(
                    $"Error processing Lightning payment: {ex.Message}");
            }
        }
    }
}

