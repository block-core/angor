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
    /// Gets the detailed swap data including the swap tree (for manual claiming)
    /// </summary>
    /// <param name="swapId">The swap ID</param>
    /// <returns>Swap details including the swap tree for claiming</returns>
    Task<Result<BoltzSubmarineSwap>> GetSwapDetailsAsync(string swapId);


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

    /// <summary>
    /// Broadcasts a signed transaction to the Bitcoin network via Boltz.
    /// </summary>
    /// <param name="transactionHex">The signed transaction hex to broadcast</param>
    /// <returns>The transaction ID if successful</returns>
    Task<Result<string>> BroadcastTransactionAsync(string transactionHex);

    /// <summary>
    /// Gets the fee information for reverse submarine swaps (Lightning → On-chain).
    /// Use this to calculate the correct invoice amount that will result in the desired on-chain amount.
    /// </summary>
    /// <returns>Fee information including percentage and miner fees</returns>
    Task<Result<BoltzSwapFees>> GetReverseSwapFeesAsync();

    /// <summary>
    /// Calculates the invoice amount needed to receive a specific on-chain amount after fees.
    /// </summary>
    /// <param name="desiredOnChainAmount">The amount you want to receive on-chain (in sats)</param>
    /// <returns>The invoice amount to pay, or failure if amount is out of limits</returns>
    Task<Result<long>> CalculateInvoiceAmountAsync(long desiredOnChainAmount);

    /// <summary>
    /// Creates a Liquid to BTC reverse submarine swap.
    /// User pays L-BTC on Liquid, receives BTC on-chain.
    /// </summary>
    /// <param name="destinationAddress">The Bitcoin address to receive the swapped funds</param>
    /// <param name="amountSats">Amount in satoshis to swap</param>
    /// <param name="claimPublicKey">Public key for claiming the on-chain funds</param>
    /// <returns>Swap details including the Liquid address to pay</returns>
    Task<Result<BoltzSubmarineSwap>> CreateLiquidToBtcSwapAsync(
        string destinationAddress,
        long amountSats,
        string claimPublicKey);

    /// <summary>
    /// Gets the fee information for Liquid to BTC reverse submarine swaps.
    /// Use this to calculate the correct amount that will result in the desired on-chain amount.
    /// </summary>
    /// <returns>Fee information including percentage and miner fees</returns>
    Task<Result<BoltzSwapFees>> GetLiquidToBtcSwapFeesAsync();

    /// <summary>
    /// Calculates the Liquid payment amount needed to receive a specific on-chain BTC amount after fees.
    /// </summary>
    /// <param name="desiredOnChainAmount">The amount you want to receive on-chain (in sats)</param>
    /// <returns>The Liquid amount to pay, or failure if amount is out of limits</returns>
    Task<Result<long>> CalculateLiquidAmountAsync(long desiredOnChainAmount);
}



