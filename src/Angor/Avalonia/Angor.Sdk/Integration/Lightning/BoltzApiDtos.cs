using System.Text.Json.Serialization;

namespace Angor.Sdk.Integration.Lightning;

/// <summary>
/// Internal HTTP request/response DTOs for the Boltz API.
/// These are serialization models only — not part of the public API surface.
/// </summary>

internal class CreateReverseSubmarineSwapRequest
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;
    [JsonPropertyName("claimPublicKey")]
    public string ClaimPublicKey { get; set; } = string.Empty;
    [JsonPropertyName("preimageHash")]
    public string PreimageHash { get; set; } = string.Empty;
    [JsonPropertyName("invoiceAmount")]
    public long InvoiceAmount { get; set; }
    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

internal class CreateReverseSwapV2Response
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("invoice")]
    public string Invoice { get; set; } = string.Empty;
    [JsonPropertyName("lockupAddress")]
    public string LockupAddress { get; set; } = string.Empty;
    [JsonPropertyName("onchainAmount")]
    public long OnchainAmount { get; set; }
    [JsonPropertyName("timeoutBlockHeight")]
    public long TimeoutBlockHeight { get; set; }
    [JsonPropertyName("swapTree")]
    public string? SwapTree { get; set; }
    [JsonPropertyName("blindingKey")]
    public string? BlindingKey { get; set; }
    [JsonPropertyName("refundPublicKey")]
    public string? RefundPublicKey { get; set; }
    [JsonPropertyName("bip21")]
    public string? Bip21 { get; set; }
}

internal class SwapStatusResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("transaction")]
    public SwapTransactionInfo? Transaction { get; set; }
    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; set; }
}

internal class SwapTransactionInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("hex")]
    public string? Hex { get; set; }
    [JsonPropertyName("confirmations")]
    public int? Confirmations { get; set; }
}

internal class ClaimRequest
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    [JsonPropertyName("transaction")]
    public string Transaction { get; set; } = string.Empty;
    [JsonPropertyName("preimage")]
    public string Preimage { get; set; } = string.Empty;
    [JsonPropertyName("pubNonce")]
    public string PubNonce { get; set; } = string.Empty;
}

internal class ClaimResponse
{
    [JsonPropertyName("pubNonce")]
    public string PubNonce { get; set; } = string.Empty;
    [JsonPropertyName("partialSignature")]
    public string PartialSignature { get; set; } = string.Empty;
}

internal class BroadcastRequest
{
    [JsonPropertyName("hex")]
    public string Hex { get; set; } = string.Empty;
}

internal class BroadcastResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

internal class ReverseSwapInfoResponse
{
    [JsonPropertyName("BTC")]
    public ReverseSwapPairs? BTC { get; set; }
}

internal class ReverseSwapPairs
{
    [JsonPropertyName("BTC")]
    public ReverseSwapPairInfo? BTC { get; set; }
}

internal class ReverseSwapPairInfo
{
    [JsonPropertyName("fees")]
    public ReverseSwapFeesInfo Fees { get; set; } = new();
    [JsonPropertyName("limits")]
    public ReverseSwapLimits Limits { get; set; } = new();
}

internal class ReverseSwapFeesInfo
{
    [JsonPropertyName("percentage")]
    public decimal Percentage { get; set; }
    [JsonPropertyName("minerFees")]
    public ReverseSwapMinerFees MinerFees { get; set; } = new();
}

internal class ReverseSwapMinerFees
{
    [JsonPropertyName("claim")]
    public long Claim { get; set; }
    [JsonPropertyName("lockup")]
    public long Lockup { get; set; }
}

internal class ReverseSwapLimits
{
    [JsonPropertyName("minimal")]
    public long Minimal { get; set; }
    [JsonPropertyName("maximal")]
    public long Maximal { get; set; }
}

