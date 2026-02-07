namespace Angor.Sdk.Integration.Lightning.Models;

/// <summary>
/// Configuration for Boltz swap service.
/// Set BaseUrl based on your environment (mainnet or testnet).
/// </summary>
public class BoltzConfiguration
{
    public const string MainnetUrl = "https://api.boltz.exchange";
    public const string TestnetUrl = "https://testnet.boltz.exchange/api";
    
    /// <summary>
    /// The Boltz API base URL. Defaults to mainnet.
    /// </summary>
    public string BaseUrl { get; set; } = MainnetUrl;
    
    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Response from creating a reverse submarine swap (Lightning → On-chain).
/// User pays Lightning invoice, receives BTC on-chain.
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
    /// On-chain address where funds will be sent after claiming
    /// </summary>
    public string Address { get; set; } = string.Empty;
    
    /// <summary>
    /// Boltz lockup address where funds are held in HTLC until claimed
    /// </summary>
    public string LockupAddress { get; set; } = string.Empty;
    
    /// <summary>
    /// Expected amount in satoshis to be received on-chain (after fees)
    /// </summary>
    public long ExpectedAmount { get; set; }
    
    /// <summary>
    /// Amount the user needs to pay via Lightning
    /// </summary>
    public long InvoiceAmount { get; set; }
    
    /// <summary>
    /// Timeout block height for the swap
    /// </summary>
    public long TimeoutBlockHeight { get; set; }
    
    /// <summary>
    /// Redeem script for the swap HTLC (legacy, use SwapTree for Taproot)
    /// </summary>
    public string RedeemScript { get; set; } = string.Empty;
    
    /// <summary>
    /// Swap tree for Taproot scripts (serialized JSON)
    /// </summary>
    public string SwapTree { get; set; } = string.Empty;
    
    /// <summary>
    /// Boltz's refund public key (needed for MuSig2 claim)
    /// </summary>
    public string RefundPublicKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Our claim public key (the one we sent when creating the swap)
    /// </summary>
    public string ClaimPublicKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Blinding key for Liquid swaps (null for BTC)
    /// </summary>
    public string? BlindingKey { get; set; }
    
    /// <summary>
    /// BIP21 payment URI (if available)
    /// </summary>
    public string Bip21 { get; set; } = string.Empty;
    
    /// <summary>
    /// The preimage (secret) used to claim the on-chain funds.
    /// IMPORTANT: Store this securely - it's needed to claim the funds!
    /// </summary>
    public string Preimage { get; set; } = string.Empty;
    
    /// <summary>
    /// SHA256 hash of the preimage, sent to Boltz when creating the swap
    /// </summary>
    public string PreimageHash { get; set; } = string.Empty;
    
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
/// Response from cooperative claim signing with Boltz (MuSig2)
/// </summary>
public class BoltzClaimResponse
{
    /// <summary>Boltz's public nonce for MuSig2 aggregation</summary>
    public string PubNonce { get; set; } = string.Empty;
    
    /// <summary>Boltz's partial signature for MuSig2 aggregation</summary>
    public string PartialSignature { get; set; } = string.Empty;
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

