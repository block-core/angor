using Angor.Sdk.Integration.Lightning.Models;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Integration.Lightning;

/// <summary>
/// Service for interacting with Boltz submarine swap API.
/// Provides direct Lightning-to-onchain swaps without intermediate custody.
/// </summary>
public interface IBoltzSwapService
{
    /// <summary>
    /// Creates a submarine swap (Lightning → On-chain).
    /// User pays the Lightning invoice, funds are sent directly to the specified on-chain address.
    /// </summary>
    /// <param name="onchainAddress">The Bitcoin address to receive the swapped funds</param>
    /// <param name="amountSats">Amount in satoshis to swap</param>
    /// <param name="refundPublicKey">Public key for refund in case swap fails</param>
    /// <returns>Swap details including the Lightning invoice to pay</returns>
    Task<Result<BoltzSubmarineSwap>> CreateSubmarineSwapAsync(
        string onchainAddress, 
        long amountSats,
        string refundPublicKey);

    /// <summary>
    /// Gets the current status of a swap
    /// </summary>
    /// <param name="swapId">The swap ID returned from CreateSubmarineSwapAsync</param>
    Task<Result<BoltzSwapStatus>> GetSwapStatusAsync(string swapId);

    /// <summary>
    /// Gets current swap pairs and their limits/fees
    /// </summary>
    Task<Result<BoltzPairInfo>> GetPairInfoAsync();

    /// <summary>
    /// Creates a reverse submarine swap (On-chain → Lightning).
    /// For future use if needed.
    /// </summary>
    Task<Result<BoltzReverseSwap>> CreateReverseSwapAsync(
        string bolt11Invoice,
        string claimPublicKey);
}

