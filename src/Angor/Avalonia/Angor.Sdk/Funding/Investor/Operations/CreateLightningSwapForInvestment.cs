using Angor.Sdk.Common;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Shared;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Creates a Boltz submarine swap for funding an investment via Lightning.
/// User pays the Lightning invoice → funds go directly on-chain to the investment address.
/// No intermediate wallet or custody required.
/// </summary>
public static class CreateLightningSwapForInvestment
{
    /// <summary>
    /// Request to create a Lightning swap for investment funding
    /// </summary>
    /// <param name="WalletId">The Angor wallet ID (used to derive refund key)</param>
    /// <param name="ProjectId">The project to invest in</param>
    /// <param name="InvestmentAmount">Amount to invest in satoshis</param>
    /// <param name="ReceivingAddress">On-chain address to receive the swapped funds</param>
    /// <param name="EstimatedFeeRateSatsPerVbyte">Estimated fee rate for the investment transaction (used to calculate total on-chain amount needed)</param>
    public record CreateLightningSwapRequest(
        WalletId WalletId,
        ProjectId ProjectId,
        Amount InvestmentAmount,
        string ReceivingAddress,
        int EstimatedFeeRateSatsPerVbyte = 2) : IRequest<Result<CreateLightningSwapResponse>>;

    /// <summary>
    /// Response containing the swap details
    /// </summary>
    /// <param name="Swap">The created submarine swap with Lightning invoice</param>
    public record CreateLightningSwapResponse(BoltzSubmarineSwap Swap);

    public class CreateLightningSwapHandler(
        IBoltzSwapService boltzSwapService,
        IBoltzSwapStorageService swapStorageService,
        IProjectService projectService,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        ILogger<CreateLightningSwapHandler> logger)
        : IRequestHandler<CreateLightningSwapRequest, Result<CreateLightningSwapResponse>>
    {
        public async Task<Result<CreateLightningSwapResponse>> Handle(
            CreateLightningSwapRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation(
                    "Creating Lightning swap for wallet {WalletId}, project {ProjectId}, investment amount: {Amount} sats",
                    request.WalletId.Value, request.ProjectId, request.InvestmentAmount.Sats);

                // Step 1: Calculate the total on-chain amount needed for the investment
                // This includes:
                // - Investment amount (what goes to the project)
                // - Angor fee (1% of investment amount)
                // - Estimated investment transaction miner fee (based on provided fee rate)
                
                const int AngorFeePercentage = 1; // 1% Angor fee
                // Estimate investment tx size at ~250 vbytes (typical for 1-in, 2-out segwit tx)
                const int EstimatedInvestmentTxVbytes = 250;
                long estimatedInvestmentTxFee = request.EstimatedFeeRateSatsPerVbyte * EstimatedInvestmentTxVbytes;
                
                long investmentAmount = request.InvestmentAmount.Sats;
                long angorFee = (investmentAmount * AngorFeePercentage) / 100;
                long totalOnChainNeeded = investmentAmount + angorFee + estimatedInvestmentTxFee;
                
                logger.LogInformation(
                    "Total on-chain amount needed: {Total} sats (investment: {Investment} + angorFee: {AngorFee} + txFee: {TxFee} @ {FeeRate} sat/vb)",
                    totalOnChainNeeded, investmentAmount, angorFee, estimatedInvestmentTxFee, request.EstimatedFeeRateSatsPerVbyte);

                // Step 2: Calculate the invoice amount needed to receive the required on-chain amount
                // Boltz deducts fees from the invoice amount, so we need to pay more
                var invoiceAmountResult = await boltzSwapService.CalculateInvoiceAmountAsync(totalOnChainNeeded);
                if (invoiceAmountResult.IsFailure)
                {
                    logger.LogError("Failed to calculate invoice amount: {Error}", invoiceAmountResult.Error);
                    return Result.Failure<CreateLightningSwapResponse>(invoiceAmountResult.Error);
                }

                var invoiceAmount = invoiceAmountResult.Value;
                var boltzFees = invoiceAmount - totalOnChainNeeded;
                
                logger.LogInformation(
                    "Invoice amount calculated: {InvoiceAmount} sats (onChainNeeded: {OnChain} + boltzFees: {BoltzFees})",
                    invoiceAmount, totalOnChainNeeded, boltzFees);

                // Step 3: Generate claim public key from wallet and project
                var claimPubKeyResult = await GenerateClaimPublicKey(request.WalletId, request.ProjectId);
                if (claimPubKeyResult.IsFailure)
                {
                    logger.LogError("Failed to generate claim public key: {Error}", claimPubKeyResult.Error);
                    return Result.Failure<CreateLightningSwapResponse>(claimPubKeyResult.Error);
                }

                var claimPubKey = claimPubKeyResult.Value;

                // Step 4: Create the reverse submarine swap with the calculated invoice amount
                var swapResult = await boltzSwapService.CreateSubmarineSwapAsync(
                    request.ReceivingAddress,
                    invoiceAmount,  // Use calculated amount that includes fees
                    claimPubKey);

                if (swapResult.IsFailure)
                {
                    logger.LogError("Failed to create swap: {Error}", swapResult.Error);
                    return Result.Failure<CreateLightningSwapResponse>(swapResult.Error);
                }

                var swap = swapResult.Value;

                // Save the swap to the database for later retrieval (claiming, status tracking)
                var saveResult = await swapStorageService.SaveSwapAsync(
                    swap, 
                    request.WalletId.Value, 
                    request.ProjectId.Value);
                
                if (saveResult.IsFailure)
                {
                    logger.LogWarning(
                        "Failed to save swap to database: {Error}. Swap created but not persisted!", 
                        saveResult.Error);
                    // Don't fail the operation - the swap was created successfully
                }

                logger.LogInformation(
                    "Lightning swap created successfully. SwapId: {SwapId}, Invoice: {Invoice}",
                    swap.Id, swap.Invoice.Substring(0, Math.Min(30, swap.Invoice.Length)) + "...");

                return Result.Success(new CreateLightningSwapResponse(swap));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating Lightning swap for wallet {WalletId}", request.WalletId.Value);
                return Result.Failure<CreateLightningSwapResponse>($"Error creating swap: {ex.Message}");
            }
        }

        private async Task<Result<string>> GenerateClaimPublicKey(WalletId walletId, ProjectId projectId)
        {
            // 1. Get project to retrieve founder key
            var projectResult = await projectService.GetAsync(projectId);
            if (projectResult.IsFailure)
            {
                return Result.Failure<string>($"Project not found: {projectResult.Error}");
            }

            var project = projectResult.Value;

            // 2. Get wallet words
            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(walletId.Value);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<string>($"Failed to get wallet data: {sensitiveDataResult.Error}");
            }

            var walletWords = sensitiveDataResult.Value.ToWalletWords();

            // 3. Derive investor key using founder key from project
            // This key is used to claim the on-chain funds from the Boltz swap
            var compressedPubKey = derivationOperations.DeriveInvestorKey(walletWords, project.FounderKey);

            // Keep the compressed format (66 chars with 02/03 prefix)
            // Boltz API requires all pubkeys to have the same length, and internally uses compressed keys
            var claimPubKey = compressedPubKey.Trim().ToLowerInvariant();

            logger.LogInformation(
                "Generated claim public key (compressed format): {Key} ({Len} chars)",
                claimPubKey, claimPubKey.Length);

            return Result.Success(claimPubKey);
        }
    }
}
