using Angor.Sdk.Common;
using Angor.Shared.Integration.Lightning;
using Angor.Shared.Integration.Lightning.Models;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Creates a Boltz reverse submarine swap (Lightning → On-chain).
/// Generic version — accepts a claim public key directly instead of deriving
/// from a project/founder key. Works for both invest and deploy flows.
///
/// The claim key is used in the Boltz HTLC for MuSig2 cooperative claiming.
/// It has no relation to the investment or project creation transaction — it
/// only needs to be a key the caller controls.
/// </summary>
public static class CreateLightningSwap
{
    /// <param name="WalletId">The wallet ID (for swap storage association)</param>
    /// <param name="ClaimPublicKey">Compressed public key hex (66 chars) used to claim the Boltz HTLC</param>
    /// <param name="Amount">Amount in satoshis to receive on-chain</param>
    /// <param name="ReceivingAddress">On-chain address to receive the swapped funds</param>
    /// <param name="StageCount">Number of stage outputs in the spending transaction (drives tx size estimate)</param>
    /// <param name="EstimatedFeeRateSatsPerVbyte">Estimated fee rate for the spending transaction</param>
    public record CreateLightningSwapRequest(
        WalletId WalletId,
        string ClaimPublicKey,
        Amount Amount,
        string ReceivingAddress,
        int StageCount,
        int EstimatedFeeRateSatsPerVbyte = 2) : IRequest<Result<CreateLightningSwapResponse>>;

    public record CreateLightningSwapResponse(BoltzSubmarineSwap Swap);

    public class CreateLightningSwapHandler(
        IBoltzSwapService boltzSwapService,
        IBoltzSwapStorageService swapStorageService,
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
                    "Creating Lightning swap for wallet {WalletId}, amount: {Amount} sats, address: {Address}",
                    request.WalletId.Value, request.Amount.Sats, request.ReceivingAddress);

                // Step 1: Calculate the total on-chain amount needed
                // Includes the requested amount + estimated spending tx miner fee + estimated claim tx fee
                const int AngorFeePercentage = 1;

                // Spending tx structure:
                //   ~10.5 vB  tx overhead
                //   ~68   vB  1 P2WPKH input
                //    43   vB  1 P2WSH output (angor fee)
                //   ~99   vB  1 OP_RETURN output
                //  N×43   vB  N P2TR stage outputs
                //    31   vB  1 P2WPKH change output
                // Total ≈ 252 + (stageCount × 43) vbytes
                int estimatedSpendingTxVbytes = 252 + (request.StageCount * 43);
                long estimatedSpendingTxFee = request.EstimatedFeeRateSatsPerVbyte * estimatedSpendingTxVbytes;

                // Boltz claim tx: ~155 vB worst case (script-path fallback)
                const int EstimatedClaimTxVbytes = 155;
                long estimatedClaimTxFee = request.EstimatedFeeRateSatsPerVbyte * EstimatedClaimTxVbytes;

                long investmentAmount = request.Amount.Sats;
                long angorFee = (investmentAmount * AngorFeePercentage) / 100;
                long totalOnChainNeeded = investmentAmount + angorFee + estimatedSpendingTxFee + estimatedClaimTxFee;

                logger.LogInformation(
                    "Total on-chain amount needed: {Total} sats (amount: {Amount} + angorFee: {AngorFee} + spendTxFee: {SpendTxFee} [{SpendVbytes} vB] + claimTxFee: {ClaimTxFee} [{ClaimVbytes} vB] @ {FeeRate} sat/vb, {Stages} stages)",
                    totalOnChainNeeded, investmentAmount, angorFee, estimatedSpendingTxFee, estimatedSpendingTxVbytes, estimatedClaimTxFee, EstimatedClaimTxVbytes, request.EstimatedFeeRateSatsPerVbyte, request.StageCount);

                // Step 2: Ensure the amount meets Boltz minimum. If below, bump up —
                // the excess stays in the wallet as change after the spending transaction.
                var feesResult = await boltzSwapService.GetReverseSwapFeesAsync();
                if (feesResult.IsSuccess && totalOnChainNeeded < feesResult.Value.MinAmount)
                {
                    logger.LogInformation(
                        "Amount {Total} sats is below Boltz minimum {Min} sats — bumping up",
                        totalOnChainNeeded, feesResult.Value.MinAmount);
                    totalOnChainNeeded = feesResult.Value.MinAmount;
                }

                // Step 3: Calculate the invoice amount (accounts for Boltz fees)
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

                // Step 3: Use the provided claim public key directly
                var claimPubKey = request.ClaimPublicKey.Trim().ToLowerInvariant();
                logger.LogInformation(
                    "Using claim public key: {Key} ({Len} chars)",
                    claimPubKey, claimPubKey.Length);

                // Step 4: Create the reverse submarine swap
                var swapResult = await boltzSwapService.CreateSubmarineSwapAsync(
                    request.ReceivingAddress,
                    invoiceAmount,
                    claimPubKey);

                if (swapResult.IsFailure)
                {
                    logger.LogError("Failed to create swap: {Error}", swapResult.Error);
                    return Result.Failure<CreateLightningSwapResponse>(swapResult.Error);
                }

                var swap = swapResult.Value;

                // Save the swap to storage
                var saveResult = await swapStorageService.SaveSwapAsync(
                    swap,
                    request.WalletId.Value,
                    "payment-flow"); // Generic context, not project-specific

                if (saveResult.IsFailure)
                {
                    logger.LogWarning("Failed to save swap: {Error}. Swap created but not persisted!", saveResult.Error);
                }

                logger.LogInformation(
                    "Lightning swap created. SwapId: {SwapId}, Invoice: {Invoice}",
                    swap.Id, swap.Invoice.Substring(0, Math.Min(30, swap.Invoice.Length)) + "...");

                return Result.Success(new CreateLightningSwapResponse(swap));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating Lightning swap");
                return Result.Failure<CreateLightningSwapResponse>($"Error creating swap: {ex.Message}");
            }
        }
    }
}
