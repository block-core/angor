using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Angor.Sdk.Integration.Lightning.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Integration.Lightning;

/// <summary>
/// Implementation of Boltz submarine swap service.
/// Provides trustless Lightning ↔ On-chain swaps without intermediate custody.
/// </summary>
public class BoltzSwapService : IBoltzSwapService
{
    private readonly HttpClient _httpClient;
    private readonly BoltzConfiguration _configuration;
    private readonly ILogger<BoltzSwapService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _apiPrefix;

    public BoltzSwapService(
        HttpClient httpClient,
        BoltzConfiguration configuration,
        ILogger<BoltzSwapService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);
        _apiPrefix = _configuration.ApiPrefix;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    /// <summary>
    /// Creates a reverse submarine swap (Lightning → On-chain).
    /// User pays a Lightning invoice, receives BTC on-chain.
    /// </summary>
    public async Task<Result<BoltzSubmarineSwap>> CreateSubmarineSwapAsync(
        string onchainAddress,
        long amountSats,
        string claimPublicKey)
    {
        try
        {
            _logger.LogInformation(
                "Creating reverse submarine swap: {Amount} sats to address {Address}",
                amountSats, onchainAddress);


            // Generate preimage (32 bytes random) and its SHA256 hash
            var preimage = GeneratePreimage();
            var preimageHash = ComputePreimageHash(preimage);

            _logger.LogDebug("Generated preimage hash: {Hash}", preimageHash);

            // Boltz API v2 reverse submarine swap request
            // User sends Lightning → receives on-chain BTC
            var request = new CreateReverseSubmarineSwapRequest
            {
                From = "BTC",           // From Lightning BTC
                To = "BTC",             // To on-chain BTC
                ClaimPublicKey = claimPublicKey,
                PreimageHash = preimageHash,
                InvoiceAmount = amountSats,
                Address = onchainAddress  // Optional: direct claim to this address
            };

            var response = await _httpClient.PostAsJsonAsync($"{_apiPrefix}swap/reverse", request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create reverse swap: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltzSubmarineSwap>($"Failed to create swap: {error}");
            }

            var swapResponse = await response.Content.ReadFromJsonAsync<CreateReverseSwapV2Response>(_jsonOptions);
            if (swapResponse == null)
            {
                return Result.Failure<BoltzSubmarineSwap>("Failed to deserialize swap response");
            }

            var swap = new BoltzSubmarineSwap
            {
                Id = swapResponse.Id,
                Invoice = swapResponse.Invoice,
                Address = onchainAddress,
                LockupAddress = swapResponse.LockupAddress,
                ExpectedAmount = swapResponse.OnchainAmount,
                InvoiceAmount = amountSats,
                TimeoutBlockHeight = swapResponse.TimeoutBlockHeight,
                RedeemScript = string.Empty, // Not used in v2 API (Taproot uses SwapTree instead)
                SwapTree = swapResponse.SwapTree != null 
                    ? JsonSerializer.Serialize(swapResponse.SwapTree, _jsonOptions) 
                    : string.Empty,
                RefundPublicKey = swapResponse.RefundPublicKey ?? string.Empty,
                ClaimPublicKey = claimPublicKey,
                BlindingKey = swapResponse.BlindingKey,
                Preimage = preimage,
                PreimageHash = preimageHash,
                Status = SwapState.Created
            };

            _logger.LogInformation(
                "Reverse submarine swap created: ID={SwapId}, Invoice amount={Amount} sats, OnchainAmount={OnchainAmount}",
                swap.Id, swap.InvoiceAmount, swap.ExpectedAmount);

            return Result.Success(swap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reverse submarine swap");
            return Result.Failure<BoltzSubmarineSwap>($"Error creating swap: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a cryptographically secure 32-byte preimage.
    /// </summary>
    private static string GeneratePreimage()
    {
        var preimage = new byte[32];
        RandomNumberGenerator.Fill(preimage);
        return Convert.ToHexString(preimage).ToLowerInvariant();
    }

    /// <summary>
    /// Computes SHA256 hash of the preimage.
    /// </summary>
    private static string ComputePreimageHash(string preimageHex)
    {
        var preimageBytes = Convert.FromHexString(preimageHex);
        var hashBytes = SHA256.HashData(preimageBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<Result<BoltzSwapStatus>> GetSwapStatusAsync(string swapId)
    {
        try
        {
            _logger.LogDebug("Getting swap status for {SwapId}", swapId);

            // Use configurable API prefix
            var response = await _httpClient.GetAsync($"{_apiPrefix}/swap/{swapId}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get swap status: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltzSwapStatus>($"Failed to get swap status: {error}");
            }

            var statusResponse = await response.Content.ReadFromJsonAsync<SwapStatusResponse>(_jsonOptions);
            if (statusResponse == null)
            {
                return Result.Failure<BoltzSwapStatus>("Failed to deserialize status response");
            }

            var status = new BoltzSwapStatus
            {
                SwapId = swapId,
                Status = ParseSwapState(statusResponse.Status),
                TransactionId = statusResponse.Transaction?.Id,
                TransactionHex = statusResponse.Transaction?.Hex,
                Confirmations = statusResponse.Transaction?.Confirmations,
                FailureReason = statusResponse.FailureReason
            };

            _logger.LogDebug("Swap {SwapId} status: {Status}", swapId, status.Status);

            return Result.Success(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting swap status for {SwapId}", swapId);
            return Result.Failure<BoltzSwapStatus>($"Error getting swap status: {ex.Message}");
        }
    }


    /// <summary>
    /// Claims the on-chain funds after the Lightning invoice has been paid.
    /// This uses cooperative MuSig2 signing with Boltz.
    /// </summary>
    /// <param name="swapId">The swap ID</param>
    /// <param name="claimTransaction">The unsigned claim transaction hex</param>
    /// <param name="preimage">The preimage (secret) for the swap</param>
    /// <param name="pubNonce">Our public nonce for MuSig2</param>
    /// <returns>Boltz's partial signature and public nonce</returns>
    public async Task<Result<BoltzClaimResponse>> GetClaimSignatureAsync(
        string swapId,
        string claimTransaction,
        string preimage,
        string pubNonce)
    {
        try
        {
            _logger.LogInformation("Getting claim signature for swap {SwapId}", swapId);

            var request = new ClaimRequest
            {
                Index = 0,
                Transaction = claimTransaction,
                Preimage = preimage,
                PubNonce = pubNonce
            };

            var response = await _httpClient.PostAsJsonAsync($"{_apiPrefix}/swap/reverse/{swapId}/claim", request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get claim signature: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltzClaimResponse>($"Failed to get claim signature: {error}");
            }

            var claimResponse = await response.Content.ReadFromJsonAsync<ClaimResponse>(_jsonOptions);
            if (claimResponse == null)
            {
                return Result.Failure<BoltzClaimResponse>("Failed to deserialize claim response");
            }

            return Result.Success(new BoltzClaimResponse
            {
                PubNonce = claimResponse.PubNonce,
                PartialSignature = claimResponse.PartialSignature
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting claim signature for {SwapId}", swapId);
            return Result.Failure<BoltzClaimResponse>($"Error getting claim signature: {ex.Message}");
        }
    }

    /// <summary>
    /// Broadcasts a signed transaction to the Bitcoin network via Boltz.
    /// </summary>
    public async Task<Result<string>> BroadcastTransactionAsync(string transactionHex)
    {
        try
        {
            _logger.LogInformation("Broadcasting transaction");

            var request = new BroadcastRequest { Hex = transactionHex };
            var response = await _httpClient.PostAsJsonAsync($"{_apiPrefix}/chain/BTC/transaction", request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to broadcast transaction: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<string>($"Failed to broadcast transaction: {error}");
            }

            var broadcastResponse = await response.Content.ReadFromJsonAsync<BroadcastResponse>(_jsonOptions);
            if (broadcastResponse == null)
            {
                return Result.Failure<string>("Failed to deserialize broadcast response");
            }

            _logger.LogInformation("Transaction broadcast successfully: {TxId}", broadcastResponse.Id);
            return Result.Success(broadcastResponse.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting transaction");
            return Result.Failure<string>($"Error broadcasting transaction: {ex.Message}");
        }
    }

    private static SwapState ParseSwapState(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "swap.created" => SwapState.Created,
            "invoice.set" => SwapState.InvoiceSet,
            "invoice.paid" => SwapState.InvoicePaid,
            "invoice.failedtopay" => SwapState.InvoiceFailedToPay,
            "invoice.expired" => SwapState.InvoiceExpired,
            "transaction.mempool" => SwapState.TransactionMempool,
            "transaction.confirmed" => SwapState.TransactionConfirmed,
            "transaction.claimed" => SwapState.TransactionClaimed,
            "transaction.refunded" => SwapState.TransactionRefunded,
            "swap.expired" => SwapState.SwapExpired,
            _ => SwapState.Created
        };
    }

    #region Request/Response DTOs

    /// <summary>
    /// Boltz API v2 submarine swap request
    /// Submarine swap: User pays Lightning → receives on-chain BTC
    /// </summary>
    private class CreateSubmarineSwapRequest
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = "BTC";  // Lightning BTC

        [JsonPropertyName("to")]
        public string To { get; set; } = "BTC";    // On-chain BTC

        [JsonPropertyName("refundPublicKey")]
        public string RefundPublicKey { get; set; } = string.Empty;

        [JsonPropertyName("invoiceAmount")]
        public long InvoiceAmount { get; set; }
    }

    /// <summary>
    /// Boltz API v2 reverse submarine swap request.
    /// Reverse swap: User pays Lightning → receives on-chain BTC
    /// </summary>
    private class CreateReverseSubmarineSwapRequest
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = "BTC";  // From Lightning BTC

        [JsonPropertyName("to")]
        public string To { get; set; } = "BTC";    // To on-chain BTC

        [JsonPropertyName("claimPublicKey")]
        public string ClaimPublicKey { get; set; } = string.Empty;

        [JsonPropertyName("preimageHash")]
        public string PreimageHash { get; set; } = string.Empty;

        [JsonPropertyName("invoiceAmount")]
        public long InvoiceAmount { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }  // Optional: direct claim address
    }

    /// <summary>
    /// Boltz API v2 reverse submarine swap response
    /// </summary>
    private class CreateReverseSwapV2Response
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
        public SwapTreeResponse? SwapTree { get; set; }

        [JsonPropertyName("refundPublicKey")]
        public string? RefundPublicKey { get; set; }

        [JsonPropertyName("blindingKey")]
        public string? BlindingKey { get; set; }
    }

    /// <summary>
    /// Taproot swap tree containing claim and refund leaf scripts
    /// </summary>
    private class SwapTreeResponse
    {
        [JsonPropertyName("claimLeaf")]
        public SwapLeafResponse ClaimLeaf { get; set; } = new();

        [JsonPropertyName("refundLeaf")]
        public SwapLeafResponse RefundLeaf { get; set; } = new();
    }

    /// <summary>
    /// Individual leaf in the swap tree (Taproot script)
    /// </summary>
    private class SwapLeafResponse
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;
    }

    private class CreateSwapResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("invoice")]
        public string Invoice { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("expectedAmount")]
        public long ExpectedAmount { get; set; }

        [JsonPropertyName("timeoutBlockHeight")]
        public long TimeoutBlockHeight { get; set; }

        [JsonPropertyName("redeemScript")]
        public string RedeemScript { get; set; } = string.Empty;

        [JsonPropertyName("bip21")]
        public string Bip21 { get; set; } = string.Empty;
    }

    private class CreateReverseSwapRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "reversesubmarine";

        [JsonPropertyName("pairId")]
        public string PairId { get; set; } = "BTC/BTC";

        [JsonPropertyName("orderSide")]
        public string OrderSide { get; set; } = "buy";

        [JsonPropertyName("claimPublicKey")]
        public string ClaimPublicKey { get; set; } = string.Empty;

        [JsonPropertyName("invoice")]
        public string Invoice { get; set; } = string.Empty;
    }

    private class CreateReverseSwapResponse
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

        [JsonPropertyName("redeemScript")]
        public string RedeemScript { get; set; } = string.Empty;
    }

    private class SwapStatusResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("transaction")]
        public TransactionInfo? Transaction { get; set; }

        [JsonPropertyName("failureReason")]
        public string? FailureReason { get; set; }
    }

    private class TransactionInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("hex")]
        public string Hex { get; set; } = string.Empty;

        [JsonPropertyName("confirmations")]
        public int Confirmations { get; set; }
    }

    private class GetPairsResponse
    {
        [JsonPropertyName("pairs")]
        public Dictionary<string, PairData> Pairs { get; set; } = new();
    }

    private class PairData
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;

        [JsonPropertyName("limits")]
        public LimitsData Limits { get; set; } = new();

        [JsonPropertyName("fees")]
        public FeesData Fees { get; set; } = new();
    }

    private class LimitsData
    {
        [JsonPropertyName("minimal")]
        public long Minimal { get; set; }

        [JsonPropertyName("maximal")]
        public long Maximal { get; set; }
    }

    private class FeesData
    {
        [JsonPropertyName("percentage")]
        public decimal Percentage { get; set; }

        [JsonPropertyName("minerFees")]
        public MinerFeesData MinerFees { get; set; } = new();
    }

    private class MinerFeesData
    {
        [JsonPropertyName("baseAsset")]
        public MinerFeeAsset BaseAsset { get; set; } = new();
    }

    private class MinerFeeAsset
    {
        [JsonPropertyName("normal")]
        public long Normal { get; set; }
    }

    // Boltz API v2 submarine info response (not currently used)
    private class SubmarineInfoResponse
    {
        [JsonPropertyName("BTC")]
        public SubmarinePairInfo? BTC { get; set; }
    }

    private class SubmarinePairInfo
    {
        [JsonPropertyName("BTC")]
        public SubmarineAssetInfo? BTC { get; set; }
    }

    private class SubmarineAssetInfo
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;

        [JsonPropertyName("limits")]
        public SubmarineLimits Limits { get; set; } = new();

        [JsonPropertyName("fees")]
        public SubmarineFees Fees { get; set; } = new();
    }

    private class SubmarineLimits
    {
        [JsonPropertyName("minimal")]
        public long Minimal { get; set; }

        [JsonPropertyName("maximal")]
        public long Maximal { get; set; }
    }

    private class SubmarineFees
    {
        [JsonPropertyName("percentage")]
        public decimal Percentage { get; set; }

        [JsonPropertyName("minerFees")]
        public long MinerFees { get; set; }
    }

    // Boltz API v2 reverse swap info response
    private class ReverseSwapInfoResponse
    {
        [JsonPropertyName("BTC")]
        public ReversePairInfo? BTC { get; set; }
    }

    private class ReversePairInfo
    {
        [JsonPropertyName("BTC")]
        public ReverseAssetInfo? BTC { get; set; }
    }

    private class ReverseAssetInfo
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;

        [JsonPropertyName("limits")]
        public ReverseLimits Limits { get; set; } = new();

        [JsonPropertyName("fees")]
        public ReverseFees Fees { get; set; } = new();
    }

    private class ReverseLimits
    {
        [JsonPropertyName("minimal")]
        public long Minimal { get; set; }

        [JsonPropertyName("maximal")]
        public long Maximal { get; set; }
    }

    private class ReverseFees
    {
        [JsonPropertyName("percentage")]
        public decimal Percentage { get; set; }

        [JsonPropertyName("minerFees")]
        public ReverseMinerFees MinerFees { get; set; } = new();
    }

    private class ReverseMinerFees
    {
        [JsonPropertyName("claim")]
        public long Claim { get; set; }

        [JsonPropertyName("lockup")]
        public long Lockup { get; set; }
    }

    // Claim transaction request/response for cooperative signing
    private class ClaimRequest
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

    private class ClaimResponse
    {
        [JsonPropertyName("pubNonce")]
        public string PubNonce { get; set; } = string.Empty;

        [JsonPropertyName("partialSignature")]
        public string PartialSignature { get; set; } = string.Empty;
    }

    // Broadcast transaction request/response
    private class BroadcastRequest
    {
        [JsonPropertyName("hex")]
        public string Hex { get; set; } = string.Empty;
    }

    private class BroadcastResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    #endregion
}

