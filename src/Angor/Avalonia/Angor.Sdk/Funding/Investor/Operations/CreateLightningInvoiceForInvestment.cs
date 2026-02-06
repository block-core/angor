using Angor.Sdk.Common;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Sdk.Wallet.Domain;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Handler for creating a Lightning invoice to fund an investment.
/// Creates a Bolt wallet if needed and generates an invoice that, when paid, will fund the investment.
/// </summary>
public static class CreateLightningInvoiceForInvestment
{
    /// <summary>
    /// Request to create a Lightning invoice for investment funding
    /// </summary>
    /// <param name="WalletId">The Angor wallet ID</param>
    /// <param name="ProjectId">The project to invest in</param>
    /// <param name="InvestmentAmount">Amount to invest in satoshis</param>
    /// <param name="Memo">Optional memo for the invoice</param>
    public record CreateLightningInvoiceRequest(
        WalletId WalletId,
        string ProjectId,
        Amount InvestmentAmount,
        string? Memo = null) : IRequest<Result<CreateLightningInvoiceResponse>>;

    /// <summary>
    /// Response containing the Lightning invoice details
    /// </summary>
    /// <param name="Invoice">The created Lightning invoice</param>
    /// <param name="BoltWalletId">The Bolt wallet ID</param>
    public record CreateLightningInvoiceResponse(
        BoltInvoice Invoice,
        string BoltWalletId);

    public class CreateLightningInvoiceHandler(
        IBoltService boltService,
        ILogger<CreateLightningInvoiceHandler> logger)
        : IRequestHandler<CreateLightningInvoiceRequest, Result<CreateLightningInvoiceResponse>>
    {
        public async Task<Result<CreateLightningInvoiceResponse>> Handle(
            CreateLightningInvoiceRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation(
                    "Creating Lightning invoice for wallet {WalletId}, project {ProjectId}, amount: {Amount} sats",
                    request.WalletId.Value, request.ProjectId, request.InvestmentAmount.Sats);

                // Create or get Bolt wallet for this Angor wallet
                var walletResult = await GetOrCreateBoltWallet(request.WalletId);
                if (walletResult.IsFailure)
                {
                    return Result.Failure<CreateLightningInvoiceResponse>(walletResult.Error);
                }

                var boltWallet = walletResult.Value;

                // Create the invoice
                var memo = request.Memo ?? $"Investment in project {request.ProjectId.Substring(0, Math.Min(8, request.ProjectId.Length))}";
                var invoiceResult = await boltService.CreateInvoiceAsync(
                    boltWallet.Id,
                    request.InvestmentAmount.Sats,
                    memo);

                if (invoiceResult.IsFailure)
                {
                    return Result.Failure<CreateLightningInvoiceResponse>(invoiceResult.Error);
                }

                logger.LogInformation(
                    "Successfully created Lightning invoice {InvoiceId} for {Amount} sats",
                    invoiceResult.Value.Id, request.InvestmentAmount.Sats);

                return Result.Success(new CreateLightningInvoiceResponse(
                    invoiceResult.Value,
                    boltWallet.Id));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating Lightning invoice for wallet {WalletId}", request.WalletId.Value);
                return Result.Failure<CreateLightningInvoiceResponse>($"Error creating Lightning invoice: {ex.Message}");
            }
        }

        private async Task<Result<BoltWallet>> GetOrCreateBoltWallet(WalletId walletId)
        {
            try
            {
                // Try to get existing wallet first
                // In a real implementation, you'd store the mapping between WalletId and BoltWallet.Id
                var walletResult = await boltService.GetWalletAsync(walletId.Value);
                
                if (walletResult.IsSuccess)
                {
                    logger.LogDebug("Found existing Bolt wallet for {WalletId}", walletId.Value);
                    return walletResult;
                }

                // Create new wallet if not found
                logger.LogInformation("Creating new Bolt wallet for {WalletId}", walletId.Value);
                return await boltService.CreateWalletAsync(walletId.Value);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting or creating Bolt wallet for {WalletId}", walletId.Value);
                return Result.Failure<BoltWallet>($"Error accessing Bolt wallet: {ex.Message}");
            }
        }
    }
}

