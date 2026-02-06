using Angor.Sdk.Integration.Lightning.Models;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Integration.Lightning;

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
    /// <param name="destinationAddress">The Bitcoin address to receive the swapped funds</param>
    /// <param name="amountSats">Amount in satoshis to swap</param>
    /// <param name="claimPublicKey">Public key for claiming the on-chain funds</param>
    /// <returns>Swap details including the Lightning invoice to pay and preimage for claiming</returns>
    Task<Result<BoltzSubmarineSwap>> CreateSubmarineSwapAsync(
        string destinationAddress, 
        long amountSats,
        string claimPublicKey);

    /// <summary>
    /// Gets the current status of a swap
    /// </summary>
    /// <param name="swapId">The swap ID returned from CreateSubmarineSwapAsync</param>
    Task<Result<BoltzSwapStatus>> GetSwapStatusAsync(string swapId);

    /// <summary>
    /// Gets current swap pairs and their limits/fees for reverse swaps
    /// </summary>
    Task<Result<BoltzPairInfo>> GetPairInfoAsync();

    /// <summary>
    /// Gets a partial signature from Boltz for cooperative claim transaction signing (MuSig2).
    /// Call this after the lockup transaction is in mempool.
    /// </summary>
    /// <param name="swapId">The swap ID</param>
    /// <param name="claimTransaction">The unsigned claim transaction hex</param>
    /// <param name="preimage">The preimage (secret) for the swap</param>
    /// <param name="pubNonce">Our public nonce for MuSig2</param>
    /// <returns>Boltz's partial signature and public nonce for aggregation</returns>
    Task<Result<BoltzClaimResponse>> GetClaimSignatureAsync(
        string swapId,
        string claimTransaction,
        string preimage,
        string pubNonce);
}

