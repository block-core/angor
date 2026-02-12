using Angor.Data.Documents.Interfaces;
using Angor.Sdk.Integration.Lightning.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Integration.Lightning;

/// <summary>
/// Document for storing Boltz swap data in the database.
/// </summary>
public class BoltzSwapDocument
{
    /// <summary>
    /// The swap ID (used as document ID)
    /// </summary>
    public string SwapId { get; set; } = string.Empty;
    
    /// <summary>
    /// The wallet ID this swap belongs to
    /// </summary>
    public string WalletId { get; set; } = string.Empty;
    
    /// <summary>
    /// The project ID (if this swap is for an investment)
    /// </summary>
    public string? ProjectId { get; set; }
    
    /// <summary>
    /// The Lightning invoice to pay
    /// </summary>
    public string Invoice { get; set; } = string.Empty;
    
    /// <summary>
    /// The destination on-chain address
    /// </summary>
    public string Address { get; set; } = string.Empty;
    
    /// <summary>
    /// The Boltz lockup address (where funds are held before claiming)
    /// </summary>
    public string LockupAddress { get; set; } = string.Empty;
    
    /// <summary>
    /// Expected amount in satoshis
    /// </summary>
    public long ExpectedAmount { get; set; }
    
    /// <summary>
    /// Invoice amount in satoshis
    /// </summary>
    public long InvoiceAmount { get; set; }
    
    /// <summary>
    /// Timeout block height for the swap
    /// </summary>
    public long TimeoutBlockHeight { get; set; }
    
    /// <summary>
    /// The swap tree (serialized JSON) containing claim and refund scripts
    /// </summary>
    public string SwapTree { get; set; } = string.Empty;
    
    /// <summary>
    /// Boltz's refund public key
    /// </summary>
    public string RefundPublicKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Our claim public key
    /// </summary>
    public string ClaimPublicKey { get; set; } = string.Empty;
    
    /// <summary>
    /// The preimage (secret) for claiming - IMPORTANT: store securely!
    /// </summary>
    public string Preimage { get; set; } = string.Empty;
    
    /// <summary>
    /// SHA256 hash of the preimage
    /// </summary>
    public string PreimageHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Current status of the swap
    /// </summary>
    public string Status { get; set; } = "created";
    
    /// <summary>
    /// The lockup transaction ID (once available)
    /// </summary>
    public string? LockupTransactionId { get; set; }
    
    /// <summary>
    /// The lockup transaction hex (for claiming)
    /// </summary>
    public string? LockupTransactionHex { get; set; }
    
    /// <summary>
    /// The claim transaction ID (once claimed)
    /// </summary>
    public string? ClaimTransactionId { get; set; }
    
    /// <summary>
    /// When the swap was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the swap was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Convert to BoltzSubmarineSwap model
    /// </summary>
    public BoltzSubmarineSwap ToSwapModel()
    {
        return new BoltzSubmarineSwap
        {
            Id = SwapId,
            Invoice = Invoice,
            Address = Address,
            LockupAddress = LockupAddress,
            ExpectedAmount = ExpectedAmount,
            InvoiceAmount = InvoiceAmount,
            TimeoutBlockHeight = TimeoutBlockHeight,
            SwapTree = SwapTree,
            RefundPublicKey = RefundPublicKey,
            ClaimPublicKey = ClaimPublicKey,
            Preimage = Preimage,
            PreimageHash = PreimageHash,
            Status = Enum.TryParse<SwapState>(Status, true, out var state) ? state : SwapState.Created
        };
    }
    
    /// <summary>
    /// Create from BoltzSubmarineSwap model
    /// </summary>
    public static BoltzSwapDocument FromSwapModel(BoltzSubmarineSwap swap, string walletId, string? projectId = null)
    {
        return new BoltzSwapDocument
        {
            SwapId = swap.Id,
            WalletId = walletId,
            ProjectId = projectId,
            Invoice = swap.Invoice,
            Address = swap.Address,
            LockupAddress = swap.LockupAddress,
            ExpectedAmount = swap.ExpectedAmount,
            InvoiceAmount = swap.InvoiceAmount,
            TimeoutBlockHeight = swap.TimeoutBlockHeight,
            SwapTree = swap.SwapTree,
            RefundPublicKey = swap.RefundPublicKey,
            ClaimPublicKey = swap.ClaimPublicKey,
            Preimage = swap.Preimage,
            PreimageHash = swap.PreimageHash,
            Status = swap.Status.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Service for storing and retrieving Boltz swap data.
/// </summary>
public interface IBoltzSwapStorageService
{
    /// <summary>
    /// Save a new swap
    /// </summary>
    Task<Result> SaveSwapAsync(BoltzSubmarineSwap swap, string walletId, string? projectId = null);
    
    /// <summary>
    /// Get a swap by its ID
    /// </summary>
    Task<Result<BoltzSwapDocument?>> GetSwapAsync(string swapId);
    
    /// <summary>
    /// Get all swaps for a wallet
    /// </summary>
    Task<Result<IEnumerable<BoltzSwapDocument>>> GetSwapsForWalletAsync(string walletId);
    
    /// <summary>
    /// Get all pending swaps (not yet claimed) for a wallet
    /// </summary>
    Task<Result<IEnumerable<BoltzSwapDocument>>> GetPendingSwapsAsync(string walletId);
    
    /// <summary>
    /// Update the status of a swap
    /// </summary>
    Task<Result> UpdateSwapStatusAsync(string swapId, string status, string? lockupTxId = null, string? lockupTxHex = null);
    
    /// <summary>
    /// Mark a swap as claimed
    /// </summary>
    Task<Result> MarkSwapClaimedAsync(string swapId, string claimTransactionId);
}

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

    public async Task<Result> UpdateSwapStatusAsync(string swapId, string status, string? lockupTxId = null, string? lockupTxHex = null)
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

    public async Task<Result> MarkSwapClaimedAsync(string swapId, string claimTransactionId)
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
