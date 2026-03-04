using System.Text.Json.Serialization;

namespace Angor.Sdk.Integration.Lightning.Models
{
    /// <summary>
    /// Boltz API v2 submarine swap request
    /// Submarine swap: User pays Lightning → receives on-chain BTC
    /// </summary>
    public class CreateSubmarineSwapRequest
    {
        [JsonPropertyName("from")] public string From { get; set; } = "BTC"; // Lightning BTC

        [JsonPropertyName("to")] public string To { get; set; } = "BTC"; // On-chain BTC

        [JsonPropertyName("refundPublicKey")] public string RefundPublicKey { get; set; } = string.Empty;

        [JsonPropertyName("invoiceAmount")] public long InvoiceAmount { get; set; }
    }

    /// <summary>
    /// Boltz API v2 reverse submarine swap request.
    /// Reverse swap: User pays Lightning → receives on-chain BTC
    /// 
    /// Required fields:
    /// - ClaimPublicKey: x-only format (64 hex chars) - used to construct Taproot swap script
    /// - PreimageHash: SHA256 hash of preimage (64 hex chars)
    /// - InvoiceAmount: Amount in satoshis
    /// 
    /// Optional fields:
    /// - Address: If provided, Boltz automatically claims to this address after payment
    /// </summary>
    public class CreateReverseSubmarineSwapRequest
    {
        [JsonPropertyName("from")] public string From { get; set; } = "BTC"; // From Lightning BTC

        [JsonPropertyName("to")] public string To { get; set; } = "BTC"; // To on-chain BTC

        [JsonPropertyName("claimPublicKey")]
        public string ClaimPublicKey { get; set; } = string.Empty; // REQUIRED: x-only format

        [JsonPropertyName("preimageHash")] public string PreimageHash { get; set; } = string.Empty;

        [JsonPropertyName("invoiceAmount")] public long InvoiceAmount { get; set; }

        [JsonPropertyName("address")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Address { get; set; } // OPTIONAL: for automatic claiming
    }

    /// <summary>
    /// Boltz API v2 reverse submarine swap response
    /// </summary>
    public class CreateReverseSwapV2Response
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

        [JsonPropertyName("invoice")] public string Invoice { get; set; } = string.Empty;

        [JsonPropertyName("lockupAddress")] public string LockupAddress { get; set; } = string.Empty;

        [JsonPropertyName("onchainAmount")] public long OnchainAmount { get; set; }

        [JsonPropertyName("timeoutBlockHeight")]
        public long TimeoutBlockHeight { get; set; }

        [JsonPropertyName("swapTree")] public SwapTreeResponse? SwapTree { get; set; }

        [JsonPropertyName("refundPublicKey")] public string? RefundPublicKey { get; set; }

        [JsonPropertyName("blindingKey")] public string? BlindingKey { get; set; }
    }

    /// <summary>
    /// Taproot swap tree containing claim and refund leaf scripts
    /// </summary>
    public class SwapTreeResponse
    {
        [JsonPropertyName("claimLeaf")] public SwapLeafResponse ClaimLeaf { get; set; } = new();

        [JsonPropertyName("refundLeaf")] public SwapLeafResponse RefundLeaf { get; set; } = new();
    }

    /// <summary>
    /// Individual leaf in the swap tree (Taproot script)
    /// </summary>
    public class SwapLeafResponse
    {
        [JsonPropertyName("version")] public int Version { get; set; }

        [JsonPropertyName("output")] public string Output { get; set; } = string.Empty;
    }

    public class CreateSwapResponse
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

        [JsonPropertyName("invoice")] public string Invoice { get; set; } = string.Empty;

        [JsonPropertyName("address")] public string Address { get; set; } = string.Empty;

        [JsonPropertyName("expectedAmount")] public long ExpectedAmount { get; set; }

        [JsonPropertyName("timeoutBlockHeight")]
        public long TimeoutBlockHeight { get; set; }

        [JsonPropertyName("redeemScript")] public string RedeemScript { get; set; } = string.Empty;

        [JsonPropertyName("bip21")] public string Bip21 { get; set; } = string.Empty;
    }

    public class CreateReverseSwapRequest
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "reversesubmarine";

        [JsonPropertyName("pairId")] public string PairId { get; set; } = "BTC/BTC";

        [JsonPropertyName("orderSide")] public string OrderSide { get; set; } = "buy";

        [JsonPropertyName("claimPublicKey")] public string ClaimPublicKey { get; set; } = string.Empty;

        [JsonPropertyName("invoice")] public string Invoice { get; set; } = string.Empty;
    }

    public class CreateReverseSwapResponse
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

        [JsonPropertyName("invoice")] public string Invoice { get; set; } = string.Empty;

        [JsonPropertyName("lockupAddress")] public string LockupAddress { get; set; } = string.Empty;

        [JsonPropertyName("onchainAmount")] public long OnchainAmount { get; set; }

        [JsonPropertyName("timeoutBlockHeight")]
        public long TimeoutBlockHeight { get; set; }

        [JsonPropertyName("redeemScript")] public string RedeemScript { get; set; } = string.Empty;
    }

    public class SwapStatusResponse
    {
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;

        [JsonPropertyName("transaction")] public TransactionInfo? Transaction { get; set; }

        [JsonPropertyName("failureReason")] public string? FailureReason { get; set; }
    }

    public class TransactionInfo
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

        [JsonPropertyName("hex")] public string Hex { get; set; } = string.Empty;

        [JsonPropertyName("confirmations")] public int Confirmations { get; set; }
    }

    public class GetPairsResponse
    {
        [JsonPropertyName("pairs")] public Dictionary<string, PairData> Pairs { get; set; } = new();
    }

    public class PairData
    {
        [JsonPropertyName("hash")] public string Hash { get; set; } = string.Empty;

        [JsonPropertyName("limits")] public LimitsData Limits { get; set; } = new();

        [JsonPropertyName("fees")] public FeesData Fees { get; set; } = new();
    }

    public class LimitsData
    {
        [JsonPropertyName("minimal")] public long Minimal { get; set; }

        [JsonPropertyName("maximal")] public long Maximal { get; set; }
    }

    public class FeesData
    {
        [JsonPropertyName("percentage")] public decimal Percentage { get; set; }

        [JsonPropertyName("minerFees")] public MinerFeesData MinerFees { get; set; } = new();
    }

    public class MinerFeesData
    {
        [JsonPropertyName("baseAsset")] public MinerFeeAsset BaseAsset { get; set; } = new();
    }

    public class MinerFeeAsset
    {
        [JsonPropertyName("normal")] public long Normal { get; set; }
    }

    // Boltz API v2 submarine info response (not currently used)
    public class SubmarineInfoResponse
    {
        [JsonPropertyName("BTC")] public SubmarinePairInfo? BTC { get; set; }
    }

    public class SubmarinePairInfo
    {
        [JsonPropertyName("BTC")] public SubmarineAssetInfo? BTC { get; set; }
    }

    public class SubmarineAssetInfo
    {
        [JsonPropertyName("hash")] public string Hash { get; set; } = string.Empty;

        [JsonPropertyName("limits")] public SubmarineLimits Limits { get; set; } = new();

        [JsonPropertyName("fees")] public SubmarineFees Fees { get; set; } = new();
    }

    public class SubmarineLimits
    {
        [JsonPropertyName("minimal")] public long Minimal { get; set; }

        [JsonPropertyName("maximal")] public long Maximal { get; set; }
    }

    public class SubmarineFees
    {
        [JsonPropertyName("percentage")] public decimal Percentage { get; set; }

        [JsonPropertyName("minerFees")] public long MinerFees { get; set; }
    }

    // Boltz API v2 reverse swap info response
    public class ReverseSwapInfoResponse
    {
        [JsonPropertyName("BTC")] public ReversePairInfo? BTC { get; set; }
    }

    public class ReversePairInfo
    {
        [JsonPropertyName("BTC")] public ReverseAssetInfo? BTC { get; set; }
    }

    public class ReverseAssetInfo
    {
        [JsonPropertyName("hash")] public string Hash { get; set; } = string.Empty;

        [JsonPropertyName("limits")] public ReverseLimits Limits { get; set; } = new();

        [JsonPropertyName("fees")] public ReverseFees Fees { get; set; } = new();
    }

    public class ReverseLimits
    {
        [JsonPropertyName("minimal")] public long Minimal { get; set; }

        [JsonPropertyName("maximal")] public long Maximal { get; set; }
    }

    public class ReverseFees
    {
        [JsonPropertyName("percentage")] public decimal Percentage { get; set; }

        [JsonPropertyName("minerFees")] public ReverseMinerFees MinerFees { get; set; } = new();
    }

    public class ReverseMinerFees
    {
        [JsonPropertyName("claim")] public long Claim { get; set; }

        [JsonPropertyName("lockup")] public long Lockup { get; set; }
    }

    // Claim transaction request/response for cooperative signing
    public class ClaimRequest
    {
        [JsonPropertyName("index")] public int Index { get; set; }

        [JsonPropertyName("transaction")] public string Transaction { get; set; } = string.Empty;

        [JsonPropertyName("preimage")] public string Preimage { get; set; } = string.Empty;

        [JsonPropertyName("pubNonce")] public string PubNonce { get; set; } = string.Empty;
    }

    public class ClaimResponse
    {
        [JsonPropertyName("pubNonce")] public string PubNonce { get; set; } = string.Empty;

        [JsonPropertyName("partialSignature")] public string PartialSignature { get; set; } = string.Empty;
    }

    // Broadcast transaction request/response
    public class BroadcastRequest
    {
        [JsonPropertyName("hex")] public string Hex { get; set; } = string.Empty;
    }

    public class BroadcastResponse
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    }
}