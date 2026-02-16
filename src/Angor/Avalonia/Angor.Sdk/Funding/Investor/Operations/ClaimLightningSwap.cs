using Angor.Sdk.Common;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Integration.Lightning;
using Angor.Shared;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Claims funds from a Boltz reverse submarine swap.
/// 
/// This handler is responsible for:
/// 1. Retrieving swap data from storage
/// 2. Deriving the claim private key from the wallet
/// 3. Delegating the actual claim to IBoltzClaimService
/// 4. Updating the swap status in storage
/// </summary>
public static class ClaimLightningSwap
{
    /// <summary>
    /// Request to claim funds from a completed Lightning swap using stored swap data.
    /// </summary>
    /// <param name="WalletId">The wallet ID that created the swap</param>
    /// <param name="SwapId">The Boltz swap ID to claim</param>
    /// <param name="LockupTransactionHex">The hex of the lockup transaction (optional if stored)</param>
    /// <param name="LockupOutputIndex">The output index in the lockup transaction (usually 0)</param>
    /// <param name="FeeRate">Fee rate in sat/vbyte for the claim transaction</param>
    public record ClaimLightningSwapByIdRequest(
        WalletId WalletId,
        string SwapId,
        string? LockupTransactionHex = null,
        int LockupOutputIndex = 0,
        long FeeRate = 2) : IRequest<Result<ClaimLightningSwapResponse>>;

    /// <summary>
    /// Response containing the broadcast claim transaction
    /// </summary>
    /// <param name="ClaimTransactionId">Transaction ID of the broadcast claim transaction</param>
    /// <param name="ClaimTransactionHex">Hex of the signed claim transaction</param>
    public record ClaimLightningSwapResponse(
        string ClaimTransactionId,
        string ClaimTransactionHex);

    /// <summary>
    /// Handler for ClaimLightningSwapByIdRequest - retrieves swap from storage and claims funds
    /// </summary>
    public class ClaimLightningSwapByIdHandler(
        IBoltzClaimService boltzClaimService,
        IBoltzSwapStorageService swapStorageService,
        IProjectService projectService,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        ILogger<ClaimLightningSwapByIdHandler> logger)
        : IRequestHandler<ClaimLightningSwapByIdRequest, Result<ClaimLightningSwapResponse>>
    {
        public async Task<Result<ClaimLightningSwapResponse>> Handle(
            ClaimLightningSwapByIdRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Claiming swap {SwapId} for wallet {WalletId}", 
                    request.SwapId, request.WalletId.Value);

                // Step 1: Get the swap from storage, validating wallet ownership
                var swapResult = await swapStorageService.GetSwapForWalletAsync(request.SwapId, request.WalletId.Value);
                if (swapResult.IsFailure)
                {
                    return Result.Failure<ClaimLightningSwapResponse>(swapResult.Error);
                }

                var swapDoc = swapResult.Value;
                var swap = swapDoc.ToSwapModel();

                // Step 2: Get founder key from project service using project ID
                if (string.IsNullOrEmpty(swapDoc.ProjectId))
                {
                    return Result.Failure<ClaimLightningSwapResponse>(
                        "Swap has no associated project ID - cannot derive claim key");
                }

                var projectResult = await projectService.GetAsync(new ProjectId(swapDoc.ProjectId));
                if (projectResult.IsFailure)
                {
                    return Result.Failure<ClaimLightningSwapResponse>(
                        $"Project not found: {projectResult.Error}");
                }

                var founderKey = projectResult.Value.FounderKey;

                // Step 3: Derive the claim private key using founder key
                var privateKeyResult = await DeriveClaimPrivateKey(request.WalletId, founderKey);
                if (privateKeyResult.IsFailure)
                {
                    return Result.Failure<ClaimLightningSwapResponse>(privateKeyResult.Error);
                }

                // Step 4: Get lockup transaction hex
                var lockupTxHex = request.LockupTransactionHex ?? swapDoc.LockupTransactionHex;
                if (string.IsNullOrEmpty(lockupTxHex))
                {
                    return Result.Failure<ClaimLightningSwapResponse>(
                        "Lockup transaction hex not available. Please provide it or fetch from a block explorer.");
                }

                // Step 5: Claim the swap using the Boltz claim service
                var claimResult = await boltzClaimService.ClaimSwapAsync(
                    swap,
                    privateKeyResult.Value,
                    lockupTxHex,
                    request.LockupOutputIndex,
                    request.FeeRate);

                if (claimResult.IsFailure)
                {
                    return Result.Failure<ClaimLightningSwapResponse>(claimResult.Error);
                }

                // Step 6: Mark swap as claimed in the database
                await swapStorageService.MarkSwapClaimedAsync(
                    request.SwapId, 
                    request.WalletId.Value, 
                    claimResult.Value.ClaimTransactionId);

                logger.LogInformation(
                    "Successfully claimed swap {SwapId}. Claim TxId: {TxId}",
                    request.SwapId, claimResult.Value.ClaimTransactionId);

                return Result.Success(new ClaimLightningSwapResponse(
                    claimResult.Value.ClaimTransactionId,
                    claimResult.Value.ClaimTransactionHex));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error claiming swap {SwapId}", request.SwapId);
                return Result.Failure<ClaimLightningSwapResponse>($"Error claiming swap: {ex.Message}");
            }
        }

        private async Task<Result<string>> DeriveClaimPrivateKey(WalletId walletId, string founderKey)
        {
            if (string.IsNullOrEmpty(founderKey))
            {
                return Result.Failure<string>("Founder key is required to derive claim key");
            }

            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(walletId.Value);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<string>($"Failed to get wallet data: {sensitiveDataResult.Error}");
            }

            var walletWords = sensitiveDataResult.Value.ToWalletWords();

            // The claim key was derived using the founder key from the project
            Key claimPrivateKey = derivationOperations.DeriveInvestorPrivateKey(walletWords, founderKey);
            
            // Get the private key bytes and convert to hex
            byte[] keyBytes = claimPrivateKey.ToBytes();
            var privateKeyHex = Encoders.Hex.EncodeData(keyBytes);
            return Result.Success(privateKeyHex);
        }
    }
}

