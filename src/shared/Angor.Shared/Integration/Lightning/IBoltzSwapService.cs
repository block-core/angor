using Angor.Shared.Integration.Lightning.Models;
using CSharpFunctionalExtensions;

namespace Angor.Shared.Integration.Lightning;

/// <summary>
/// Service for interacting with Boltz swap API.
/// Provides trustless Lightning-to-onchain swaps (reverse submarine swaps).
/// </summary>
public interface IBoltzSwapService
{
    /// <summary>
    /// Creates a reverse submarine swap (Lightning → On-chain).
    /// User pays the Lightning invoice, funds are claimed to the destination address.
    /// </summary>
    Task<Result<BoltzSubmarineSwap>> CreateSubmarineSwapAsync(
        string destinationAddress,
        long amountSats,
        string claimPublicKey);

    /// <summary>Gets the current status of a swap</summary>
    Task<Result<BoltzSwapStatus>> GetSwapStatusAsync(string swapId);

    /// <summary>Gets the detailed swap data including the swap tree (for manual claiming)</summary>
    Task<Result<BoltzSubmarineSwap>> GetSwapDetailsAsync(string swapId);

    /// <summary>
    /// Gets a partial signature from Boltz for cooperative claim transaction signing (MuSig2).
    /// Call this after the lockup transaction is in mempool.
    /// </summary>
    Task<Result<BoltzClaimResponse>> GetClaimSignatureAsync(
        string swapId,
        string claimTransaction,
        string preimage,
        string pubNonce);

    /// <summary>Broadcasts a signed transaction to the Bitcoin network via Boltz.</summary>
    Task<Result<string>> BroadcastTransactionAsync(string transactionHex);

    /// <summary>
    /// Gets the fee information for reverse submarine swaps (Lightning → On-chain).
    /// Use this to calculate the correct invoice amount that will result in the desired on-chain amount.
    /// </summary>
    Task<Result<BoltzSwapFees>> GetReverseSwapFeesAsync();

    /// <summary>
    /// Calculates the invoice amount needed to receive a specific on-chain amount after fees.
    /// </summary>
    Task<Result<long>> CalculateInvoiceAmountAsync(long desiredOnChainAmount);
}

