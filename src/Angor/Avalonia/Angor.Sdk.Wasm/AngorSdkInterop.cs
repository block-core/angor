using System.Text.Json;
using Microsoft.JSInterop;

namespace Angor.Sdk.Wasm;

/// <summary>
/// JavaScript interop bindings for Angor SDK.
/// Methods decorated with [JSInvokable] are callable from TypeScript/JavaScript.
/// </summary>
public class AngorSdkInterop
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initialize the SDK. Must be called before using other methods.
    /// </summary>
    [JSInvokable]
    public static string Initialize(string network)
    {
        try
        {
            // TODO: Initialize SDK services with the specified network (mainnet/testnet)
            return JsonSerializer.Serialize(new { success = true, message = "SDK initialized", network }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions);
        }
    }

    /// <summary>
    /// Generate a new wallet with seed words.
    /// </summary>
    [JSInvokable]
    public static string GenerateWallet(int wordCount = 12)
    {
        try
        {
            // TODO: Use Angor.Sdk wallet generation
            // var hdOperations = new HdOperations();
            // var words = hdOperations.GenerateWords(wordCount);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true, 
                // seedWords = words,
                message = "Wallet generation not yet implemented",
                wordCount
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions);
        }
    }

    /// <summary>
    /// Get project details by project ID.
    /// </summary>
    [JSInvokable]
    public static async Task<string> GetProject(string projectId)
    {
        try
        {
            // TODO: Use IProjectService to fetch project
            await Task.CompletedTask;
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "GetProject not yet implemented",
                projectId
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions);
        }
    }

    /// <summary>
    /// Create an investment transaction draft.
    /// Note: Using double for satoshi amounts for JavaScript compatibility.
    /// JavaScript's Number can safely represent integers up to 2^53-1, which is ~90 million BTC in sats.
    /// </summary>
    [JSInvokable]
    public static async Task<string> CreateInvestment(
        string walletId,
        string projectId,
        double amountSats,
        double feeRateSatsPerVb)
    {
        try
        {
            // Convert from double to long for internal use
            var amountSatsLong = (long)amountSats;
            var feeRateLong = (long)feeRateSatsPerVb;
            
            // TODO: Use IInvestmentAppService to create investment
            await Task.CompletedTask;
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "CreateInvestment not yet implemented",
                walletId,
                projectId,
                amountSats = amountSatsLong,
                feeRateSatsPerVb = feeRateLong
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions);
        }
    }

    /// <summary>
    /// Sign a transaction with wallet credentials.
    /// </summary>
    [JSInvokable]
    public static string SignTransaction(string transactionHex, string walletSeedWords)
    {
        try
        {
            // TODO: Use signing services
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "SignTransaction not yet implemented"
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions);
        }
    }

    /// <summary>
    /// Broadcast a signed transaction.
    /// </summary>
    [JSInvokable]
    public static async Task<string> BroadcastTransaction(string signedTransactionHex)
    {
        try
        {
            // TODO: Use ITransactionService to broadcast
            await Task.CompletedTask;
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "BroadcastTransaction not yet implemented"
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions);
        }
    }

    /// <summary>
    /// Derive project keys for a founder.
    /// </summary>
    [JSInvokable]
    public static string DeriveProjectKeys(string walletSeedWords, string angorRootKey)
    {
        try
        {
            // TODO: Use IDerivationOperations
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "DeriveProjectKeys not yet implemented"
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions);
        }
    }
}
