using Angor.Sdk.Common;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Sdk.Wallet.Domain;
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
    /// <param name="PairInfo">Current swap fees and limits</param>
    public record CreateLightningSwapResponse(
        BoltzSubmarineSwap Swap,
        BoltzPairInfo PairInfo);

    public class CreateLightningSwapHandler(
        IBoltzSwapService boltzSwapService,
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

                // Get pair info first to show fees to user
                var pairResult = await boltzSwapService.GetPairInfoAsync();
                if (pairResult.IsFailure)
                {
                    logger.LogError("Failed to get pair info: {Error}", pairResult.Error);
                    return Result.Failure<CreateLightningSwapResponse>(pairResult.Error);
                }

                var pairInfo = pairResult.Value;

                // Validate amount is within limits
                if (request.InvestmentAmount.Sats < pairInfo.MinAmount)
                {
                    return Result.Failure<CreateLightningSwapResponse>(
                        $"Amount too small. Minimum: {pairInfo.MinAmount} sats");
                }

                if (request.InvestmentAmount.Sats > pairInfo.MaxAmount)
                {
                    return Result.Failure<CreateLightningSwapResponse>(
                        $"Amount too large. Maximum: {pairInfo.MaxAmount} sats");
                }

                // Generate refund public key from wallet and project
                var refundPubKeyResult = await GenerateRefundPublicKey(request.WalletId, request.ProjectId);
                if (refundPubKeyResult.IsFailure)
                {
                    logger.LogError("Failed to generate refund public key: {Error}", refundPubKeyResult.Error);
                    return Result.Failure<CreateLightningSwapResponse>(refundPubKeyResult.Error);
                }

                var refundPubKey = refundPubKeyResult.Value;

                // Create the submarine swap
                var swapResult = await boltzSwapService.CreateSubmarineSwapAsync(
                    request.ReceivingAddress,
                    request.InvestmentAmount.Sats,
                    refundPubKey);

                if (swapResult.IsFailure)
                {
                    logger.LogError("Failed to create swap: {Error}", swapResult.Error);
                    return Result.Failure<CreateLightningSwapResponse>(swapResult.Error);
                }

                var swap = swapResult.Value;

                logger.LogInformation(
                    "Lightning swap created successfully. SwapId: {SwapId}, Invoice: {Invoice}",
                    swap.Id, swap.Invoice.Substring(0, Math.Min(30, swap.Invoice.Length)) + "...");

                return Result.Success(new CreateLightningSwapResponse(swap, pairInfo));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating Lightning swap for wallet {WalletId}", request.WalletId.Value);
                return Result.Failure<CreateLightningSwapResponse>($"Error creating swap: {ex.Message}");
            }
        }

        private async Task<Result<string>> GenerateRefundPublicKey(WalletId walletId, ProjectId projectId)
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
            // This is the same key derivation used for investments, ensuring consistency
            var refundPubKey = derivationOperations.DeriveInvestorKey(walletWords, project.FounderKey);

            logger.LogDebug(
                "Generated refund public key for wallet {WalletId}, project {ProjectId}",
                walletId.Value, projectId);

            return Result.Success(refundPubKey);
        }
    }
}

