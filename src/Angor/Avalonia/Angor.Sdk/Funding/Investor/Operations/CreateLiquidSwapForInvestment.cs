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
/// Creates a Boltz Liquid→BTC swap for funding an investment.
/// User pays L-BTC on Liquid → funds arrive on-chain at the investment address.
/// No intermediate wallet or custody required.
/// </summary>
public static class CreateLiquidSwapForInvestment
{
    /// <summary>
    /// Request to create a Liquid swap for investment funding
    /// </summary>
    /// <param name="WalletId">The Angor wallet ID (used to derive claim key)</param>
    /// <param name="ProjectId">The project to invest in</param>
    /// <param name="InvestmentAmount">Amount to invest in satoshis</param>
    /// <param name="ReceivingAddress">On-chain BTC address to receive the swapped funds</param>
    /// <param name="EstimatedFeeRateSatsPerVbyte">Estimated fee rate for the investment transaction</param>
    public record CreateLiquidSwapRequest(
        WalletId WalletId,
        ProjectId ProjectId,
        Amount InvestmentAmount,
        string ReceivingAddress,
        int EstimatedFeeRateSatsPerVbyte = 2) : IRequest<Result<CreateLiquidSwapResponse>>;

    /// <summary>
    /// Response containing the swap details
    /// </summary>
    /// <param name="Swap">The created swap with Liquid address to pay</param>
    public record CreateLiquidSwapResponse(BoltzSubmarineSwap Swap);

    public class CreateLiquidSwapHandler(
        IBoltzSwapService boltzSwapService,
        IBoltzSwapStorageService swapStorageService,
        IProjectService projectService,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        ILogger<CreateLiquidSwapHandler> logger)
        : IRequestHandler<CreateLiquidSwapRequest, Result<CreateLiquidSwapResponse>>
    {
        public async Task<Result<CreateLiquidSwapResponse>> Handle(
            CreateLiquidSwapRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation(
                    "Creating Liquid swap for wallet {WalletId}, project {ProjectId}, investment amount: {Amount} sats",
                    request.WalletId.Value, request.ProjectId, request.InvestmentAmount.Sats);

                // Step 1: Calculate the total on-chain amount needed for the investment
                const int AngorFeePercentage = 1;
                const int EstimatedInvestmentTxVbytes = 250;
                long estimatedInvestmentTxFee = request.EstimatedFeeRateSatsPerVbyte * EstimatedInvestmentTxVbytes;
                
                long investmentAmount = request.InvestmentAmount.Sats;
                long angorFee = (investmentAmount * AngorFeePercentage) / 100;
                long totalOnChainNeeded = investmentAmount + angorFee + estimatedInvestmentTxFee;
                
                logger.LogInformation(
                    "Total on-chain amount needed: {Total} sats (investment: {Investment} + angorFee: {AngorFee} + txFee: {TxFee} @ {FeeRate} sat/vb)",
                    totalOnChainNeeded, investmentAmount, angorFee, estimatedInvestmentTxFee, request.EstimatedFeeRateSatsPerVbyte);

                // Step 2: Calculate the Liquid amount needed to receive the required on-chain amount
                var liquidAmountResult = await boltzSwapService.CalculateLiquidAmountAsync(totalOnChainNeeded);
                if (liquidAmountResult.IsFailure)
                {
                    logger.LogError("Failed to calculate Liquid amount: {Error}", liquidAmountResult.Error);
                    return Result.Failure<CreateLiquidSwapResponse>(liquidAmountResult.Error);
                }

                var liquidAmount = liquidAmountResult.Value;
                var boltzFees = liquidAmount - totalOnChainNeeded;
                
                logger.LogInformation(
                    "Liquid amount calculated: {LiquidAmount} sats (onChainNeeded: {OnChain} + boltzFees: {BoltzFees})",
                    liquidAmount, totalOnChainNeeded, boltzFees);

                // Step 3: Generate claim public key from wallet and project
                var claimPubKeyResult = await GenerateClaimPublicKey(request.WalletId, request.ProjectId);
                if (claimPubKeyResult.IsFailure)
                {
                    logger.LogError("Failed to generate claim public key: {Error}", claimPubKeyResult.Error);
                    return Result.Failure<CreateLiquidSwapResponse>(claimPubKeyResult.Error);
                }

                var claimPubKey = claimPubKeyResult.Value;

                // Step 4: Create the Liquid→BTC swap
                var swapResult = await boltzSwapService.CreateLiquidToBtcSwapAsync(
                    request.ReceivingAddress,
                    liquidAmount,
                    claimPubKey);

                if (swapResult.IsFailure)
                {
                    logger.LogError("Failed to create swap: {Error}", swapResult.Error);
                    return Result.Failure<CreateLiquidSwapResponse>(swapResult.Error);
                }

                var swap = swapResult.Value;

                // Save the swap to the database
                var saveResult = await swapStorageService.SaveSwapAsync(
                    swap, 
                    request.WalletId.Value, 
                    request.ProjectId.Value);
                
                if (saveResult.IsFailure)
                {
                    logger.LogWarning(
                        "Failed to save swap to database: {Error}. Swap created but not persisted!", 
                        saveResult.Error);
                }

                logger.LogInformation(
                    "Liquid swap created successfully. SwapId: {SwapId}, LiquidAddress: {Address}",
                    swap.Id, swap.LockupAddress);

                return Result.Success(new CreateLiquidSwapResponse(swap));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating Liquid swap for wallet {WalletId}", request.WalletId.Value);
                return Result.Failure<CreateLiquidSwapResponse>($"Error creating swap: {ex.Message}");
            }
        }

        private async Task<Result<string>> GenerateClaimPublicKey(WalletId walletId, ProjectId projectId)
        {
            var projectResult = await projectService.GetAsync(projectId);
            if (projectResult.IsFailure)
            {
                return Result.Failure<string>($"Project not found: {projectResult.Error}");
            }

            var project = projectResult.Value;

            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(walletId.Value);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<string>($"Failed to get wallet data: {sensitiveDataResult.Error}");
            }

            var walletWords = sensitiveDataResult.Value.ToWalletWords();

            var compressedPubKey = derivationOperations.DeriveInvestorKey(walletWords, project.FounderKey);
            var claimPubKey = compressedPubKey.Trim().ToLowerInvariant();

            logger.LogInformation(
                "Generated claim public key (compressed format): {Key} ({Len} chars)",
                claimPubKey, claimPubKey.Length);

            return Result.Success(claimPubKey);
        }
    }
}

