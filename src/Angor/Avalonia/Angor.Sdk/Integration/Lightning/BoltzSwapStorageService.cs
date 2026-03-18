using Angor.Data.Documents.Interfaces;
using Angor.Shared.Integration.Lightning;
using Angor.Shared.Integration.Lightning.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Integration.Lightning;

/// <summary>
/// Implementation of Boltz swap storage using the document database.
/// </summary>
public class BoltzSwapStorageService(
    IGenericDocumentCollection<BoltzSwapDocument> collection,
    ILogger<BoltzSwapStorageService> logger) : IBoltzSwapStorageService
{
    public async Task<Result> SaveSwapAsync(BoltzSubmarineSwap swap, string walletId, string? projectId = null)
    {
        try
        {
            var doc = BoltzSwapDocument.FromSwapModel(swap, walletId, projectId);
            
            var result = await collection.UpsertAsync(d => d.SwapId, doc);
            
            if (result.IsFailure)
            {
                logger.LogError("Failed to save swap {SwapId}: {Error}", swap.Id, result.Error);
                return Result.Failure($"Failed to save swap: {result.Error}");
            }
            
            logger.LogInformation("Saved swap {SwapId} for wallet {WalletId}", swap.Id, walletId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving swap {SwapId}", swap.Id);
            return Result.Failure($"Error saving swap: {ex.Message}");
        }
    }

    public async Task<Result<BoltzSwapDocument?>> GetSwapAsync(string swapId)
    {
        try
        {
            var result = await collection.FindByIdAsync(swapId);
            
            if (result.IsFailure)
            {
                logger.LogError("Failed to get swap {SwapId}: {Error}", swapId, result.Error);
                return Result.Failure<BoltzSwapDocument?>($"Failed to get swap: {result.Error}");
            }
            
            return Result.Success(result.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting swap {SwapId}", swapId);
            return Result.Failure<BoltzSwapDocument?>($"Error getting swap: {ex.Message}");
        }
    }

    public async Task<Result<BoltzSwapDocument>> GetSwapForWalletAsync(string swapId, string walletId)
    {
        try
        {
            var result = await collection.FindAsync(d => d.SwapId == swapId && d.WalletId == walletId);
            
            if (result.IsFailure || result.Value == null || !result.Value.Any())
            {
                logger.LogError("Swap {SwapId} not found for wallet {WalletId}", swapId, walletId);
                return Result.Failure<BoltzSwapDocument>("Swap not found or does not belong to the specified wallet.");
            }
            
            return Result.Success(result.Value.First());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting swap {SwapId} for wallet {WalletId}", swapId, walletId);
            return Result.Failure<BoltzSwapDocument>($"Error getting swap: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<BoltzSwapDocument>>> GetSwapsForWalletAsync(string walletId)
    {
        try
        {
            var result = await collection.FindAsync(d => d.WalletId == walletId);
            
            if (result.IsFailure)
            {
                logger.LogError("Failed to get swaps for wallet {WalletId}: {Error}", walletId, result.Error);
                return Result.Failure<IEnumerable<BoltzSwapDocument>>($"Failed to get swaps: {result.Error}");
            }
            
            return Result.Success(result.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting swaps for wallet {WalletId}", walletId);
            return Result.Failure<IEnumerable<BoltzSwapDocument>>($"Error getting swaps: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<BoltzSwapDocument>>> GetPendingSwapsAsync(string walletId)
    {
        try
        {
            // Get swaps that are confirmed but not yet claimed
            var result = await collection.FindAsync(d => 
                d.WalletId == walletId && 
                d.ClaimTransactionId == null &&
                (d.Status == "TransactionConfirmed" || 
                 d.Status == "TransactionMempool" ||
                 d.Status == "transaction.confirmed" ||
                 d.Status == "transaction.mempool"));
            
            if (result.IsFailure)
            {
                logger.LogError("Failed to get pending swaps for wallet {WalletId}: {Error}", walletId, result.Error);
                return Result.Failure<IEnumerable<BoltzSwapDocument>>($"Failed to get pending swaps: {result.Error}");
            }
            
            return Result.Success(result.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting pending swaps for wallet {WalletId}", walletId);
            return Result.Failure<IEnumerable<BoltzSwapDocument>>($"Error getting pending swaps: {ex.Message}");
        }
    }

    public async Task<Result> UpdateSwapStatusAsync(string swapId, string walletId, string status, string? lockupTxId = null, string? lockupTxHex = null)
    {
        try
        {
            var getResult = await collection.FindByIdAsync(swapId);
            
            if (getResult.IsFailure || getResult.Value == null)
            {
                logger.LogError("Swap {SwapId} not found for status update", swapId);
                return Result.Failure($"Swap not found: {swapId}");
            }
            
            var doc = getResult.Value;
            
            // Validate wallet ownership
            if (doc.WalletId != walletId)
            {
                logger.LogError("Swap {SwapId} belongs to wallet {SwapWalletId}, not {RequestedWalletId}", 
                    swapId, doc.WalletId, walletId);
                return Result.Failure($"Swap {swapId} does not belong to this wallet");
            }
            
            doc.Status = status;
            doc.UpdatedAt = DateTime.UtcNow;
            
            if (!string.IsNullOrEmpty(lockupTxId))
                doc.LockupTransactionId = lockupTxId;
            
            if (!string.IsNullOrEmpty(lockupTxHex))
                doc.LockupTransactionHex = lockupTxHex;
            
            var updateResult = await collection.UpdateAsync(d => d.SwapId, doc);
            
            if (updateResult.IsFailure)
            {
                logger.LogError("Failed to update swap status {SwapId}: {Error}", swapId, updateResult.Error);
                return Result.Failure($"Failed to update swap status: {updateResult.Error}");
            }
            
            logger.LogInformation("Updated swap {SwapId} status to {Status}", swapId, status);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating swap status {SwapId}", swapId);
            return Result.Failure($"Error updating swap status: {ex.Message}");
        }
    }

    public async Task<Result> MarkSwapClaimedAsync(string swapId, string walletId, string claimTransactionId)
    {
        try
        {
            var getResult = await collection.FindByIdAsync(swapId);
            
            if (getResult.IsFailure || getResult.Value == null)
            {
                logger.LogError("Swap {SwapId} not found for marking as claimed", swapId);
                return Result.Failure($"Swap not found: {swapId}");
            }
            
            var doc = getResult.Value;
            
            // Validate wallet ownership
            if (doc.WalletId != walletId)
            {
                logger.LogError("Swap {SwapId} belongs to wallet {SwapWalletId}, not {RequestedWalletId}", 
                    swapId, doc.WalletId, walletId);
                return Result.Failure($"Swap {swapId} does not belong to this wallet");
            }
            
            doc.Status = "Claimed";
            doc.ClaimTransactionId = claimTransactionId;
            doc.UpdatedAt = DateTime.UtcNow;
            
            var updateResult = await collection.UpdateAsync(d => d.SwapId, doc);
            
            if (updateResult.IsFailure)
            {
                logger.LogError("Failed to mark swap as claimed {SwapId}: {Error}", swapId, updateResult.Error);
                return Result.Failure($"Failed to mark swap as claimed: {updateResult.Error}");
            }
            
            logger.LogInformation("Marked swap {SwapId} as claimed with tx {TxId}", swapId, claimTransactionId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error marking swap as claimed {SwapId}", swapId);
            return Result.Failure($"Error marking swap as claimed: {ex.Message}");
        }
    }
}
