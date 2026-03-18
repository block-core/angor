using Angor.Shared.Integration.Lightning;
using Angor.Shared.Integration.Lightning.Models;
using Blazored.LocalStorage;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Client.Storage;

/// <summary>
/// Browser localStorage-based implementation of <see cref="IBoltzSwapStorageService"/>.
/// Swaps are stored as individual items keyed by <c>boltz-swap:{swapId}</c>.
/// A per-wallet index (<c>boltz-wallet-swaps:{walletId}</c>) keeps track of swap IDs
/// belonging to each wallet for efficient lookup.
/// </summary>
public class BoltzSwapStorageService : IBoltzSwapStorageService
{
    private const string SwapKeyPrefix = "boltz-swap:";
    private const string WalletSwapsKeyPrefix = "boltz-wallet-swaps:";

    private readonly ISyncLocalStorageService _storage;
    private readonly ILogger<BoltzSwapStorageService> _logger;

    public BoltzSwapStorageService(ISyncLocalStorageService storage, ILogger<BoltzSwapStorageService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public Task<Result> SaveSwapAsync(BoltzSubmarineSwap swap, string walletId, string? projectId = null)
    {
        try
        {
            var doc = BoltzSwapDocument.FromSwapModel(swap, walletId, projectId);

            _storage.SetItem(SwapKey(doc.SwapId), doc);

            // Update wallet index
            var index = GetWalletIndex(walletId);
            if (!index.Contains(doc.SwapId))
            {
                index.Add(doc.SwapId);
                _storage.SetItem(WalletSwapsKey(walletId), index);
            }

            _logger.LogInformation("Saved swap {SwapId} for wallet {WalletId}", swap.Id, walletId);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving swap {SwapId}", swap.Id);
            return Task.FromResult(Result.Failure($"Error saving swap: {ex.Message}"));
        }
    }

    public Task<Result<BoltzSwapDocument?>> GetSwapAsync(string swapId)
    {
        try
        {
            var doc = _storage.GetItem<BoltzSwapDocument>(SwapKey(swapId));
            return Task.FromResult(Result.Success<BoltzSwapDocument?>(doc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting swap {SwapId}", swapId);
            return Task.FromResult(Result.Failure<BoltzSwapDocument?>($"Error getting swap: {ex.Message}"));
        }
    }

    public Task<Result<BoltzSwapDocument>> GetSwapForWalletAsync(string swapId, string walletId)
    {
        try
        {
            var doc = _storage.GetItem<BoltzSwapDocument>(SwapKey(swapId));

            if (doc == null)
            {
                _logger.LogError("Swap {SwapId} not found for wallet {WalletId}", swapId, walletId);
                return Task.FromResult(Result.Failure<BoltzSwapDocument>("Swap not found or does not belong to the specified wallet."));
            }

            if (doc.WalletId != walletId)
            {
                _logger.LogError("Swap {SwapId} belongs to wallet {SwapWalletId}, not {RequestedWalletId}",
                    swapId, doc.WalletId, walletId);
                return Task.FromResult(Result.Failure<BoltzSwapDocument>("Swap not found or does not belong to the specified wallet."));
            }

            return Task.FromResult(Result.Success(doc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting swap {SwapId} for wallet {WalletId}", swapId, walletId);
            return Task.FromResult(Result.Failure<BoltzSwapDocument>($"Error getting swap: {ex.Message}"));
        }
    }

    public Task<Result<IEnumerable<BoltzSwapDocument>>> GetSwapsForWalletAsync(string walletId)
    {
        try
        {
            var docs = LoadSwapsForWallet(walletId);
            return Task.FromResult(Result.Success<IEnumerable<BoltzSwapDocument>>(docs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting swaps for wallet {WalletId}", walletId);
            return Task.FromResult(Result.Failure<IEnumerable<BoltzSwapDocument>>($"Error getting swaps: {ex.Message}"));
        }
    }

    public Task<Result<IEnumerable<BoltzSwapDocument>>> GetPendingSwapsAsync(string walletId)
    {
        try
        {
            var docs = LoadSwapsForWallet(walletId)
                .Where(d => d.ClaimTransactionId == null &&
                            (d.Status == "TransactionConfirmed" ||
                             d.Status == "TransactionMempool" ||
                             d.Status == "transaction.confirmed" ||
                             d.Status == "transaction.mempool"));

            return Task.FromResult(Result.Success<IEnumerable<BoltzSwapDocument>>(docs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending swaps for wallet {WalletId}", walletId);
            return Task.FromResult(Result.Failure<IEnumerable<BoltzSwapDocument>>($"Error getting pending swaps: {ex.Message}"));
        }
    }

    public Task<Result> UpdateSwapStatusAsync(string swapId, string walletId, string status,
        string? lockupTxId = null, string? lockupTxHex = null)
    {
        try
        {
            var doc = _storage.GetItem<BoltzSwapDocument>(SwapKey(swapId));

            if (doc == null)
            {
                _logger.LogError("Swap {SwapId} not found for status update", swapId);
                return Task.FromResult(Result.Failure($"Swap not found: {swapId}"));
            }

            if (doc.WalletId != walletId)
            {
                _logger.LogError("Swap {SwapId} belongs to wallet {SwapWalletId}, not {RequestedWalletId}",
                    swapId, doc.WalletId, walletId);
                return Task.FromResult(Result.Failure($"Swap {swapId} does not belong to this wallet"));
            }

            doc.Status = status;
            doc.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(lockupTxId))
                doc.LockupTransactionId = lockupTxId;

            if (!string.IsNullOrEmpty(lockupTxHex))
                doc.LockupTransactionHex = lockupTxHex;

            _storage.SetItem(SwapKey(swapId), doc);

            _logger.LogInformation("Updated swap {SwapId} status to {Status}", swapId, status);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating swap status {SwapId}", swapId);
            return Task.FromResult(Result.Failure($"Error updating swap status: {ex.Message}"));
        }
    }

    public Task<Result> MarkSwapClaimedAsync(string swapId, string walletId, string claimTransactionId)
    {
        try
        {
            var doc = _storage.GetItem<BoltzSwapDocument>(SwapKey(swapId));

            if (doc == null)
            {
                _logger.LogError("Swap {SwapId} not found for marking as claimed", swapId);
                return Task.FromResult(Result.Failure($"Swap not found: {swapId}"));
            }

            if (doc.WalletId != walletId)
            {
                _logger.LogError("Swap {SwapId} belongs to wallet {SwapWalletId}, not {RequestedWalletId}",
                    swapId, doc.WalletId, walletId);
                return Task.FromResult(Result.Failure($"Swap {swapId} does not belong to this wallet"));
            }

            doc.Status = "Claimed";
            doc.ClaimTransactionId = claimTransactionId;
            doc.UpdatedAt = DateTime.UtcNow;

            _storage.SetItem(SwapKey(swapId), doc);

            _logger.LogInformation("Marked swap {SwapId} as claimed with tx {TxId}", swapId, claimTransactionId);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking swap as claimed {SwapId}", swapId);
            return Task.FromResult(Result.Failure($"Error marking swap as claimed: {ex.Message}"));
        }
    }

    #region Helpers

    private static string SwapKey(string swapId) => $"{SwapKeyPrefix}{swapId}";
    private static string WalletSwapsKey(string walletId) => $"{WalletSwapsKeyPrefix}{walletId}";

    private List<string> GetWalletIndex(string walletId)
    {
        return _storage.GetItem<List<string>>(WalletSwapsKey(walletId)) ?? new List<string>();
    }

    private List<BoltzSwapDocument> LoadSwapsForWallet(string walletId)
    {
        var index = GetWalletIndex(walletId);
        var results = new List<BoltzSwapDocument>();

        foreach (var swapId in index)
        {
            var doc = _storage.GetItem<BoltzSwapDocument>(SwapKey(swapId));
            if (doc != null)
                results.Add(doc);
        }

        return results;
    }

    #endregion
}

