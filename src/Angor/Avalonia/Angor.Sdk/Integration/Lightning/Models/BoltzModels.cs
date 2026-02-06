namespace Angor.Sdk.Integration.Lightning.Models;

/// <summary>
/// Configuration for Boltz swap service
/// </summary>
public class BoltzConfiguration
{
    public string BaseUrl { get; set; } = "https://api.boltz.exchange";
    public string TestnetBaseUrl { get; set; } = "https://testnet.boltz.exchange/api";
    public bool UseTestnet { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 30;
    
    public string GetActiveBaseUrl() => UseTestnet ? TestnetBaseUrl : BaseUrl;
}

/// <summary>
/// Response from creating a submarine swap (Lightning → On-chain)
/// </summary>
public class BoltzSubmarineSwap
{
    /// <summary>
    /// Unique identifier for this swap
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Lightning invoice (BOLT11) that the user needs to pay
    /// </summary>
    public string Invoice { get; set; } = string.Empty;
    
    /// <summary>
    /// On-chain address where funds will be sent after payment
    /// </summary>
    public string Address { get; set; } = string.Empty;
    
    /// <summary>
    /// Expected amount in satoshis to be received on-chain
    /// </summary>
    public long ExpectedAmount { get; set; }
    
    /// <summary>
    /// Amount the user needs to pay (including fees)
    /// </summary>
    public long InvoiceAmount { get; set; }
    
    /// <summary>
    /// Timeout block height for the swap
    /// </summary>
    public long TimeoutBlockHeight { get; set; }
    
    /// <summary>
    /// Redeem script for the swap HTLC
    /// </summary>
    public string RedeemScript { get; set; } = string.Empty;
    
    /// <summary>
    /// BIP21 payment URI
    /// </summary>
    public string Bip21 { get; set; } = string.Empty;
    
    /// <summary>
    /// Swap status
    /// </summary>
    public SwapState Status { get; set; } = SwapState.Created;
}

/// <summary>
/// Response from creating a reverse submarine swap (On-chain → Lightning)
/// </summary>
public class BoltzReverseSwap
{
    public string Id { get; set; } = string.Empty;
    public string Invoice { get; set; } = string.Empty;
    public string LockupAddress { get; set; } = string.Empty;
    public long OnchainAmount { get; set; }
    public long TimeoutBlockHeight { get; set; }
    public string RedeemScript { get; set; } = string.Empty;
}

/// <summary>
/// Current status of a swap
/// </summary>
public class BoltzSwapStatus
{
    public string SwapId { get; set; } = string.Empty;
    public SwapState Status { get; set; }
    public string? TransactionId { get; set; }
    public string? TransactionHex { get; set; }
    public int? Confirmations { get; set; }
    public string? FailureReason { get; set; }
}

/// <summary>
/// Swap state enum matching Boltz API states
/// </summary>
public enum SwapState
{
    /// <summary>Swap was created</summary>
    Created,
    
    /// <summary>Invoice was set (for reverse swaps)</summary>
    InvoiceSet,
    
    /// <summary>Invoice was paid, waiting for on-chain confirmation</summary>
    InvoicePaid,
    
    /// <summary>Invoice payment failed</summary>
    InvoiceFailedToPay,
    
    /// <summary>Invoice expired</summary>
    InvoiceExpired,
    
    /// <summary>On-chain transaction is in mempool</summary>
    TransactionMempool,
    
    /// <summary>On-chain transaction confirmed</summary>
    TransactionConfirmed,
    
    /// <summary>Swap completed successfully</summary>
    TransactionClaimed,
    
    /// <summary>Swap was refunded</summary>
    TransactionRefunded,
    
    /// <summary>Swap failed</summary>
    SwapExpired
}

/// <summary>
/// Information about a trading pair (e.g., BTC/BTC for Lightning ↔ On-chain)
/// </summary>
public class BoltzPairInfo
{
    public string PairId { get; set; } = "BTC/BTC";
    
    /// <summary>Minimum swap amount in satoshis</summary>
    public long MinAmount { get; set; }
    
    /// <summary>Maximum swap amount in satoshis</summary>
    public long MaxAmount { get; set; }
    
    /// <summary>Fee percentage (e.g., 0.5 = 0.5%)</summary>
    public decimal FeePercentage { get; set; }
    
    /// <summary>Miner fee in satoshis</summary>
    public long MinerFee { get; set; }
    
    /// <summary>Hash of the pair configuration</summary>
    public string Hash { get; set; } = string.Empty;
}

/// <summary>
/// Extension methods for swap states
/// </summary>
public static class SwapStateExtensions
{
    public static bool IsComplete(this SwapState state) =>
        state == SwapState.TransactionClaimed;

    public static bool IsFailed(this SwapState state) =>
        state is SwapState.InvoiceFailedToPay 
            or SwapState.InvoiceExpired 
            or SwapState.SwapExpired 
            or SwapState.TransactionRefunded;

    public static bool IsPending(this SwapState state) =>
        state is SwapState.Created 
            or SwapState.InvoiceSet 
            or SwapState.InvoicePaid 
            or SwapState.TransactionMempool 
            or SwapState.TransactionConfirmed;
}

