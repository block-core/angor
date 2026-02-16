using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Shared;
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
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly ILogger<BoltzSwapService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _apiPrefix;

    public BoltzSwapService(
        HttpClient httpClient,
        BoltzConfiguration configuration,
        INetworkConfiguration networkConfiguration,
        ILogger<BoltzSwapService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _networkConfiguration = networkConfiguration ?? throw new ArgumentNullException(nameof(networkConfiguration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);
        _apiPrefix = _configuration.ApiPrefix;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    /// <summary>
    /// Creates a reverse submarine swap (Lightning → On-chain).
    /// User pays a Lightning invoice, receives BTC on-chain.
    /// 
    /// When an onchainAddress is provided, Boltz performs automatic claiming.
    /// The claimPublicKey is stored for reference but NOT sent to the API when using automatic claim.
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

            // Boltz v2 API accepts compressed public keys (66 hex chars, 33 bytes with 02/03 prefix).
            // The refundPublicKey returned by Boltz is also in compressed format.
            // Just ensure lowercase for consistency.
            var normalizedClaimPubKey = claimPublicKey.Trim().ToLowerInvariant();
            
            _logger.LogDebug(
                "Claim public key: {Key} ({Len} chars)",
                normalizedClaimPubKey, normalizedClaimPubKey.Length);

            // Validate the key is compressed (66 chars) or x-only (64 chars)
            if (normalizedClaimPubKey.Length != 66 && normalizedClaimPubKey.Length != 64)
            {
                _logger.LogError(
                    "Invalid claim public key length: {Length} chars (expected 66 or 64). Key: {Key}",
                    normalizedClaimPubKey.Length, normalizedClaimPubKey);
                return Result.Failure<BoltzSubmarineSwap>(
                    $"Invalid claim public key: expected 66 or 64 hex chars, got {normalizedClaimPubKey.Length}");
            }

            // Generate preimage (32 bytes random) and its SHA256 hash
            var preimage = GeneratePreimage();
            var preimageHash = ComputePreimageHash(preimage);

            _logger.LogDebug("Generated preimage hash: {Hash}", preimageHash);

            // Boltz API v2 reverse submarine swap request
            // claimPublicKey is REQUIRED - used to construct the Taproot swap script
            // address is OPTIONAL - if provided, Boltz automatically claims to this address
            //
            // NOTE: Boltz expects 'address' to be a standard Bitcoin address string (Bech32/Base58),
            // NOT a scriptPubKey hex. Boltz handles the address conversion internally.
            
            _logger.LogDebug(
                "Using address for automatic claim: {Address}",
                onchainAddress);
            
            var request = new CreateReverseSubmarineSwapRequest
            {
                From = "BTC",           // From Lightning BTC
                To = "BTC",             // To on-chain BTC
                ClaimPublicKey = normalizedClaimPubKey,  // REQUIRED: x-only format for Taproot
                PreimageHash = preimageHash,
                InvoiceAmount = amountSats,
                Address = onchainAddress  // Standard Bitcoin address (Bech32)
            };

            // Log the request for debugging
            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            _logger.LogInformation(
                "Sending reverse swap request (automatic claim mode) - " +
                "PreimageHash: {PreimageHash} ({HashLen} chars), Amount: {Amount}, Address: {Address}",
                preimageHash, preimageHash.Length,
                amountSats, onchainAddress);
            _logger.LogInformation("Request JSON: {Json}", requestJson);

            // Use explicit JSON content to ensure our serialization options are applied
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_apiPrefix}swap/reverse", content);

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
                ClaimPublicKey = normalizedClaimPubKey,
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

    /// <summary>
    /// Normalizes a public key to x-only format (32 bytes, 64 hex chars, lowercase).
    /// Boltz V2 Taproot API requires x-only public keys.
    /// </summary>
    private static string NormalizePublicKey(string publicKeyHex)
    {
        var key = publicKeyHex.Trim();
        
        // If it's a compressed key (33 bytes = 66 hex chars with 02/03 prefix), strip the prefix
        if (key.Length == 66 && 
            (key.StartsWith("02", StringComparison.OrdinalIgnoreCase) || 
             key.StartsWith("03", StringComparison.OrdinalIgnoreCase)))
        {
            key = key[2..];
        }
        
        // Ensure lowercase for Boltz API
        return key.ToLowerInvariant();
    }


    public async Task<Result<BoltzSwapStatus>> GetSwapStatusAsync(string swapId)
    {
        try
        {
            _logger.LogDebug("Getting swap status for {SwapId}", swapId);

            // Use configurable API prefix
            var response = await _httpClient.GetAsync($"{_apiPrefix}swap/{swapId}");

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

    public async Task<Result<BoltzSubmarineSwap>> GetSwapDetailsAsync(string swapId)
    {
        try
        {
            _logger.LogDebug("Getting swap details for {SwapId}", swapId);

            // Use the swap/reverse/{id} endpoint to get full swap details including tree
            var response = await _httpClient.GetAsync($"{_apiPrefix}swap/reverse/{swapId}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get swap details: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltzSubmarineSwap>($"Failed to get swap details: {error}");
            }

            var swapResponse = await response.Content.ReadFromJsonAsync<CreateReverseSwapV2Response>(_jsonOptions);
            if (swapResponse == null)
            {
                return Result.Failure<BoltzSubmarineSwap>("Failed to deserialize swap details response");
            }

            var swap = new BoltzSubmarineSwap
            {
                Id = swapResponse.Id,
                Invoice = swapResponse.Invoice,
                LockupAddress = swapResponse.LockupAddress,
                ExpectedAmount = swapResponse.OnchainAmount,
                TimeoutBlockHeight = swapResponse.TimeoutBlockHeight,
                SwapTree = swapResponse.SwapTree != null 
                    ? JsonSerializer.Serialize(swapResponse.SwapTree, _jsonOptions) 
                    : string.Empty,
                RefundPublicKey = swapResponse.RefundPublicKey ?? string.Empty,
            };

            _logger.LogDebug("Got swap details for {SwapId}, SwapTree: {HasSwapTree}", 
                swapId, !string.IsNullOrEmpty(swap.SwapTree));

            return Result.Success(swap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting swap details for {SwapId}", swapId);
            return Result.Failure<BoltzSubmarineSwap>($"Error getting swap details: {ex.Message}");
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

            var response = await _httpClient.PostAsJsonAsync($"{_apiPrefix}swap/reverse/{swapId}/claim", request, _jsonOptions);

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
            _logger.LogInformation("Broadcasting transaction via Boltz API");

            var request = new BroadcastRequest { Hex = transactionHex };
            // Note: _apiPrefix is "v2/" so we don't add another slash
            var response = await _httpClient.PostAsJsonAsync($"{_apiPrefix}chain/BTC/transaction", request, _jsonOptions);

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

    /// <summary>
    /// Gets the fee information for reverse submarine swaps (Lightning → On-chain).
    /// </summary>
    public async Task<Result<BoltzSwapFees>> GetReverseSwapFeesAsync()
    {
        try
        {
            _logger.LogDebug("Fetching reverse swap fee information from Boltz");

            // Boltz v2 API endpoint for reverse swap info
            var response = await _httpClient.GetAsync($"{_apiPrefix}swap/reverse");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get reverse swap fees: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltzSwapFees>($"Failed to get swap fees: {error}");
            }

            var feesResponse = await response.Content.ReadFromJsonAsync<ReverseSwapInfoResponse>(_jsonOptions);
            if (feesResponse == null)
            {
                return Result.Failure<BoltzSwapFees>("Failed to deserialize fees response");
            }

            // Navigate nested structure: response.BTC.BTC contains the actual fee info
            var btcInfo = feesResponse.BTC?.BTC;
            if (btcInfo == null)
            {
                return Result.Failure<BoltzSwapFees>("Invalid fees response: missing BTC pair info");
            }

            var fees = new BoltzSwapFees
            {
                Percentage = btcInfo.Fees.Percentage,
                MinerFees = btcInfo.Fees.MinerFees.Claim,
                MinAmount = btcInfo.Limits.Minimal,
                MaxAmount = btcInfo.Limits.Maximal
            };

            _logger.LogDebug(
                "Reverse swap fees: {Percentage}% + {MinerFees} sats, limits: {Min}-{Max}",
                fees.Percentage, fees.MinerFees, fees.MinAmount, fees.MaxAmount);

            return Result.Success(fees);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reverse swap fees");
            return Result.Failure<BoltzSwapFees>($"Error getting swap fees: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculates the invoice amount needed to receive a specific on-chain amount after fees.
    /// </summary>
    public async Task<Result<long>> CalculateInvoiceAmountAsync(long desiredOnChainAmount)
    {
        var feesResult = await GetReverseSwapFeesAsync();
        if (feesResult.IsFailure)
        {
            return Result.Failure<long>(feesResult.Error);
        }

        var fees = feesResult.Value;
        var invoiceAmount = fees.CalculateInvoiceAmount(desiredOnChainAmount);

        // Validate against limits
        if (invoiceAmount < fees.MinAmount)
        {
            return Result.Failure<long>($"Invoice amount {invoiceAmount} sats is below minimum {fees.MinAmount} sats");
        }
        if (invoiceAmount > fees.MaxAmount)
        {
            return Result.Failure<long>($"Invoice amount {invoiceAmount} sats exceeds maximum {fees.MaxAmount} sats");
        }

        _logger.LogDebug(
            "Calculated invoice amount: {InvoiceAmount} sats to receive {OnChainAmount} sats on-chain",
            invoiceAmount, desiredOnChainAmount);

        return Result.Success(invoiceAmount);
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
    /// 
    /// Required fields:
    /// - ClaimPublicKey: x-only format (64 hex chars) - used to construct Taproot swap script
    /// - PreimageHash: SHA256 hash of preimage (64 hex chars)
    /// - InvoiceAmount: Amount in satoshis
    /// 
    /// Optional fields:
    /// - Address: If provided, Boltz automatically claims to this address after payment
    /// </summary>
    private class CreateReverseSubmarineSwapRequest
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = "BTC";  // From Lightning BTC

        [JsonPropertyName("to")]
        public string To { get; set; } = "BTC";    // To on-chain BTC

        [JsonPropertyName("claimPublicKey")]
        public string ClaimPublicKey { get; set; } = string.Empty;  // REQUIRED: x-only format

        [JsonPropertyName("preimageHash")]
        public string PreimageHash { get; set; } = string.Empty;

        [JsonPropertyName("invoiceAmount")]
        public long InvoiceAmount { get; set; }

        [JsonPropertyName("address")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Address { get; set; }  // OPTIONAL: for automatic claiming
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

