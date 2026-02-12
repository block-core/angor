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
    public record CreateLightningSwapRequest(
        WalletId WalletId,
        ProjectId ProjectId,
        Amount InvestmentAmount,
        string ReceivingAddress) : IRequest<Result<CreateLightningSwapResponse>>;

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
                    "Creating Lightning swap for wallet {WalletId}, project {ProjectId}, amount: {Amount} sats",
                    request.WalletId.Value, request.ProjectId, request.InvestmentAmount.Sats);

                // Generate claim public key from wallet and project
                var claimPubKeyResult = await GenerateClaimPublicKey(request.WalletId, request.ProjectId);
                if (claimPubKeyResult.IsFailure)
                {
                    logger.LogError("Failed to generate claim public key: {Error}", claimPubKeyResult.Error);
                    return Result.Failure<CreateLightningSwapResponse>(claimPubKeyResult.Error);
                }

                var claimPubKey = claimPubKeyResult.Value;

                // Create the reverse submarine swap
                var swapResult = await boltzSwapService.CreateSubmarineSwapAsync(
                    request.ReceivingAddress,
                    request.InvestmentAmount.Sats,
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
