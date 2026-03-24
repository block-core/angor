using Angor.Shared.Integration.Lightning.Models;
using CSharpFunctionalExtensions;

namespace Angor.Shared.Integration.Lightning;

/// <summary>
/// Service for storing and retrieving Boltz swap data.
/// </summary>
public interface IBoltzSwapStorageService
{
    /// <summary>Save a new swap</summary>
    Task<Result> SaveSwapAsync(BoltzSubmarineSwap swap, string walletId, string? projectId = null);

    /// <summary>
    /// Get a swap by its ID (without wallet validation - use GetSwapForWalletAsync for secure access)
    /// </summary>
    Task<Result<BoltzSwapDocument?>> GetSwapAsync(string swapId);

    /// <summary>
    /// Get a swap by its ID, validating that it belongs to the specified wallet.
    /// Returns failure if the swap doesn't exist or belongs to a different wallet.
    /// </summary>
    Task<Result<BoltzSwapDocument>> GetSwapForWalletAsync(string swapId, string walletId);

    /// <summary>Get all swaps for a wallet</summary>
    Task<Result<IEnumerable<BoltzSwapDocument>>> GetSwapsForWalletAsync(string walletId);

    /// <summary>Get all pending swaps (not yet claimed) for a wallet</summary>
    Task<Result<IEnumerable<BoltzSwapDocument>>> GetPendingSwapsAsync(string walletId);

    /// <summary>Update the status of a swap, validating wallet ownership</summary>
    Task<Result> UpdateSwapStatusAsync(string swapId, string walletId, string status, string? lockupTxId = null, string? lockupTxHex = null);

    /// <summary>Mark a swap as claimed, validating wallet ownership</summary>
    Task<Result> MarkSwapClaimedAsync(string swapId, string walletId, string claimTransactionId);
}

