using Angor.Sdk.Integration.Lightning.Models;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Integration.Lightning;

/// <summary>
/// Service for claiming funds from Boltz reverse submarine swaps.
/// This encapsulates all the Taproot/MuSig2 claim logic for Boltz swaps.
/// </summary>
public interface IBoltzClaimService
{
    /// <summary>
    /// Claims funds from a completed reverse submarine swap.
    /// Attempts cooperative MuSig2 claim first, falls back to script path spend.
    /// </summary>
    /// <param name="swap">The swap data including preimage, keys, and swap tree</param>
    /// <param name="claimPrivateKeyHex">The private key hex for signing the claim</param>
    /// <param name="lockupTransactionHex">The lockup transaction hex</param>
    /// <param name="lockupOutputIndex">The output index in the lockup transaction (usually 0)</param>
    /// <param name="feeRate">Fee rate in sat/vbyte for the claim transaction</param>
    /// <returns>The claim transaction ID and hex if successful</returns>
    Task<Result<BoltzClaimResult>> ClaimSwapAsync(
        BoltzSubmarineSwap swap,
        string claimPrivateKeyHex,
        string lockupTransactionHex,
        int lockupOutputIndex = 0,
        long feeRate = 2);
}

/// <summary>
/// Result of a successful swap claim
/// </summary>
public record BoltzClaimResult(
    string ClaimTransactionId,
    string ClaimTransactionHex);

