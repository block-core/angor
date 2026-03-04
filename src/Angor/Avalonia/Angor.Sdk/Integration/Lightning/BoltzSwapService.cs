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
    private readonly JsonSerializerOptions _deserializeOptions;
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

        // Options for serializing requests (uses camelCase)
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        
        // Options for deserializing responses (no naming policy, relies on JsonPropertyName attributes)
        _deserializeOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
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

            // Log the raw response for debugging
            var rawResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Lightning reverse swap raw response: {Response}", rawResponse);

            // Diagnostic: Parse with JsonDocument to see all properties
            try
            {
                using var doc = JsonDocument.Parse(rawResponse);
                var root = doc.RootElement;
                var propertyNames = string.Join(", ", root.EnumerateObject().Select(p => $"{p.Name}={p.Value.ValueKind}"));
                _logger.LogInformation("Response properties: {Properties}", propertyNames);
                
                if (root.TryGetProperty("invoice", out var invoiceProp))
                {
                    _logger.LogInformation("Found 'invoice' property with value length: {Length}", invoiceProp.GetString()?.Length ?? 0);
                }
                else
                {
                    _logger.LogWarning("'invoice' property not found in response!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse response diagnostically");
            }

            var swapResponse = JsonSerializer.Deserialize<CreateReverseSwapV2Response>(rawResponse, _deserializeOptions);
            if (swapResponse == null)
            {
                _logger.LogError("Failed to deserialize Lightning swap response. Raw: {Response}", rawResponse);
                return Result.Failure<BoltzSubmarineSwap>("Failed to deserialize swap response");
            }
            
            _logger.LogInformation("Deserialized swap - Id: {Id}, Invoice length: {InvoiceLen}, LockupAddress: {LockupAddress}", 
                swapResponse.Id, swapResponse.Invoice?.Length ?? 0, swapResponse.LockupAddress);

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

            var swapResponse = await response.Content.ReadFromJsonAsync<CreateReverseSwapV2Response>(_deserializeOptions);
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
            "transaction.server.mempool" => SwapState.TransactionServerMempool,
            "transaction.server.confirmed" => SwapState.TransactionServerConfirmed,
            "transaction.claim.pending" => SwapState.TransactionClaimPending,
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
        
        [JsonPropertyName("L-BTC")]
        public ReversePairInfo? LBTC { get; set; }
    }

    private class ReversePairInfo
    {
        [JsonPropertyName("BTC")]
        public PairInfo? BTC { get; set; }
        
        [JsonPropertyName("L-BTC")]
        public PairInfo? LBTC { get; set; }
    }

    /// <summary>
    /// Generic pair info for swap fees and limits
    /// </summary>
    private class PairInfo
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

    #region Liquid (Chain) Swap Methods

    /// <summary>
    /// Creates a Liquid to BTC chain swap.
    /// User pays L-BTC on Liquid, receives BTC on-chain.
    /// Uses POST /swap/chain endpoint.
    /// </summary>
    public async Task<Result<BoltzSubmarineSwap>> CreateLiquidToBtcSwapAsync(
        string onchainAddress,
        long amountSats,
        string claimPublicKey,
        string refundPublicKey)
    {
        try
        {
            _logger.LogInformation(
                "Creating Liquid→BTC chain swap: {Amount} sats to address {Address}",
                amountSats, onchainAddress);

            var normalizedClaimPubKey = claimPublicKey.Trim().ToLowerInvariant();
            var normalizedRefundPubKey = refundPublicKey.Trim().ToLowerInvariant();

            if (normalizedClaimPubKey.Length != 66 && normalizedClaimPubKey.Length != 64)
            {
                return Result.Failure<BoltzSubmarineSwap>(
                    $"Invalid claim public key: expected 66 or 64 hex chars, got {normalizedClaimPubKey.Length}");
            }

            if (normalizedRefundPubKey.Length != 66 && normalizedRefundPubKey.Length != 64)
            {
                return Result.Failure<BoltzSubmarineSwap>(
                    $"Invalid refund public key: expected 66 or 64 hex chars, got {normalizedRefundPubKey.Length}");
            }

            // Generate preimage and hash
            var preimage = GeneratePreimage();
            var preimageHash = ComputePreimageHash(preimage);

            _logger.LogDebug("Generated preimage hash: {Hash}", preimageHash);

            // Chain swap: from L-BTC to BTC
            var request = new CreateChainSwapRequest
            {
                From = "L-BTC",
                To = "BTC",
                ClaimPublicKey = normalizedClaimPubKey,
                RefundPublicKey = normalizedRefundPubKey,
                PreimageHash = preimageHash,
                UserLockAmount = amountSats
            };

            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            _logger.LogInformation(
                "Sending Liquid→BTC chain swap request - PreimageHash: {PreimageHash}, Amount: {Amount}, Address: {Address}",
                preimageHash, amountSats, onchainAddress);
            _logger.LogDebug("Request JSON: {Json}", requestJson);

            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_apiPrefix}swap/chain", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create Liquid→BTC chain swap: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltzSubmarineSwap>($"Failed to create swap: {error}");
            }

            var rawResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Liquid→BTC chain swap raw response: {Response}", rawResponse);

            var swapResponse = JsonSerializer.Deserialize<CreateChainSwapResponse>(rawResponse, _deserializeOptions);
            if (swapResponse == null)
            {
                _logger.LogError("Failed to deserialize chain swap response. Raw: {Response}", rawResponse);
                return Result.Failure<BoltzSubmarineSwap>("Failed to deserialize swap response");
            }

            var swap = new BoltzSubmarineSwap
            {
                Id = swapResponse.Id,
                Invoice = string.Empty, // No Lightning invoice for chain swaps
                Address = onchainAddress,
                // LockupAddress = the Liquid address where user sends L-BTC
                LockupAddress = swapResponse.LockupDetails.LockupAddress,
                // ExpectedAmount = the BTC amount Boltz will lock for the user to claim
                ExpectedAmount = swapResponse.ClaimDetails.Amount,
                InvoiceAmount = amountSats,
                TimeoutBlockHeight = swapResponse.LockupDetails.TimeoutBlockHeight,
                RedeemScript = string.Empty,
                // SwapTree for the claim side (BTC) - needed for claiming
                SwapTree = swapResponse.ClaimDetails.SwapTree != null
                    ? JsonSerializer.Serialize(swapResponse.ClaimDetails.SwapTree, _jsonOptions)
                    : string.Empty,
                // Boltz's server public key for the claim side (needed for MuSig2 claim)
                RefundPublicKey = swapResponse.ClaimDetails.ServerPublicKey ?? string.Empty,
                ClaimPublicKey = normalizedClaimPubKey,
                // BlindingKey from the lockup side (Liquid) for confidential transactions
                BlindingKey = swapResponse.LockupDetails.BlindingKey,
                Preimage = preimage,
                PreimageHash = preimageHash,
                Status = SwapState.Created,
                // Chain swap specific fields
                IsChainSwap = true,
                LockupSwapTree = swapResponse.LockupDetails.SwapTree != null
                    ? JsonSerializer.Serialize(swapResponse.LockupDetails.SwapTree, _jsonOptions)
                    : string.Empty,
                LockupServerPublicKey = swapResponse.LockupDetails.ServerPublicKey ?? string.Empty,
                ClaimLockupAddress = swapResponse.ClaimDetails.LockupAddress
            };

            _logger.LogInformation(
                "Liquid→BTC chain swap created: ID={SwapId}, LiquidLockupAddress={LiquidAddress}, " +
                "UserLockAmount={UserLockAmount}, ServerLockAmount={ServerLockAmount}",
                swap.Id, swap.LockupAddress, amountSats, swap.ExpectedAmount);

            return Result.Success(swap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Liquid→BTC chain swap");
            return Result.Failure<BoltzSubmarineSwap>($"Error creating swap: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the fee information for Liquid to BTC chain swaps.
    /// Uses GET /swap/chain endpoint.
    /// </summary>
    public async Task<Result<BoltzSwapFees>> GetLiquidToBtcSwapFeesAsync()
    {
        try
        {
            _logger.LogDebug("Fetching Liquid→BTC chain swap fee information from Boltz");

            var response = await _httpClient.GetAsync($"{_apiPrefix}swap/chain");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get Liquid→BTC chain swap fees: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltzSwapFees>($"Failed to get swap fees: {error}");
            }

            var feesResponse = await response.Content.ReadFromJsonAsync<ChainSwapInfoResponse>(_deserializeOptions);
            if (feesResponse == null)
            {
                return Result.Failure<BoltzSwapFees>("Failed to deserialize fees response");
            }

            // Navigate: response["L-BTC"]["BTC"] for L-BTC→BTC chain swap fees
            var liquidBtcInfo = feesResponse.LBTC?.BTC;
            if (liquidBtcInfo == null)
            {
                return Result.Failure<BoltzSwapFees>("Invalid fees response: missing L-BTC/BTC pair info");
            }

            // For chain swaps, the server miner fee is deducted from the swap amount.
            // User also pays user.lockup (on L-BTC) and user.claim (on BTC) separately as tx fees.
            var fees = new BoltzSwapFees
            {
                Percentage = liquidBtcInfo.Fees.Percentage,
                MinerFees = liquidBtcInfo.Fees.MinerFees.Server,
                MinAmount = liquidBtcInfo.Limits.Minimal,
                MaxAmount = liquidBtcInfo.Limits.Maximal
            };

            _logger.LogDebug(
                "Liquid→BTC chain swap fees: {Percentage}% + {ServerMinerFees} sats (server), " +
                "user claim: {UserClaim} sats, user lockup: {UserLockup} sats, limits: {Min}-{Max}",
                fees.Percentage, fees.MinerFees,
                liquidBtcInfo.Fees.MinerFees.User.Claim,
                liquidBtcInfo.Fees.MinerFees.User.Lockup,
                fees.MinAmount, fees.MaxAmount);

            return Result.Success(fees);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Liquid→BTC chain swap fees");
            return Result.Failure<BoltzSwapFees>($"Error getting swap fees: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculates the Liquid amount needed to receive a specific on-chain BTC amount after fees.
    /// </summary>
    public async Task<Result<long>> CalculateLiquidAmountAsync(long desiredOnChainAmount)
    {
        var feesResult = await GetLiquidToBtcSwapFeesAsync();
        if (feesResult.IsFailure)
        {
            return Result.Failure<long>(feesResult.Error);
        }

        var fees = feesResult.Value;
        var liquidAmount = fees.CalculateInvoiceAmount(desiredOnChainAmount);

        if (liquidAmount < fees.MinAmount)
        {
            return Result.Failure<long>($"Liquid amount {liquidAmount} sats is below minimum {fees.MinAmount} sats");
        }
        if (liquidAmount > fees.MaxAmount)
        {
            return Result.Failure<long>($"Liquid amount {liquidAmount} sats exceeds maximum {fees.MaxAmount} sats");
        }

        _logger.LogDebug(
            "Calculated Liquid amount: {LiquidAmount} sats to receive {OnChainAmount} sats on-chain",
            liquidAmount, desiredOnChainAmount);

        return Result.Success(liquidAmount);
    }

    /// <summary>
    /// Gets Boltz's chain claim details (nonce, tx hash) for cooperative signing.
    /// GET /swap/chain/{id}/claim
    /// </summary>
    public async Task<Result<ChainClaimDetails>> GetChainClaimDetailsAsync(string swapId)
    {
        try
        {
            _logger.LogInformation("Getting chain claim details for swap {SwapId}", swapId);

            var response = await _httpClient.GetAsync($"{_apiPrefix}swap/chain/{swapId}/claim");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get chain claim details: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<ChainClaimDetails>($"Failed to get chain claim details: {error}");
            }

            var rawResponse = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Chain claim details response: {Response}", rawResponse);

            var details = JsonSerializer.Deserialize<ChainClaimDetailsResponse>(rawResponse, _deserializeOptions);
            if (details == null)
            {
                return Result.Failure<ChainClaimDetails>("Failed to deserialize chain claim details");
            }

            return Result.Success(new ChainClaimDetails
            {
                PubNonce = details.PubNonce ?? string.Empty,
                PublicKey = details.PublicKey ?? string.Empty,
                TransactionHash = details.TransactionHash ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chain claim details for swap {SwapId}", swapId);
            return Result.Failure<ChainClaimDetails>($"Error getting chain claim details: {ex.Message}");
        }
    }

    /// <summary>
    /// Posts cooperative chain claim data to Boltz.
    /// POST /swap/chain/{id}/claim
    /// </summary>
    public async Task<Result<BoltzClaimResponse>> PostChainClaimAsync(string swapId, ChainClaimRequest request)
    {
        try
        {
            _logger.LogInformation("Posting chain claim for swap {SwapId}", swapId);

            var apiRequest = new ChainClaimApiRequest
            {
                Preimage = request.Preimage,
                Signature = new ChainClaimSignatureDto
                {
                    PubNonce = request.Signature.PubNonce,
                    PartialSignature = request.Signature.PartialSignature
                },
                ToSign = new ChainClaimToSignDto
                {
                    PubNonce = request.ToSign.PubNonce,
                    Transaction = request.ToSign.Transaction,
                    Index = request.ToSign.Index
                }
            };

            var requestJson = JsonSerializer.Serialize(apiRequest, _jsonOptions);
            _logger.LogDebug("Chain claim request JSON: {Json}", requestJson);

            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_apiPrefix}swap/chain/{swapId}/claim", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to post chain claim: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltzClaimResponse>($"Failed to post chain claim: {error}");
            }

            var rawResponse = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Chain claim response: {Response}", rawResponse);

            var claimResponse = JsonSerializer.Deserialize<ClaimResponse>(rawResponse, _deserializeOptions);
            if (claimResponse == null)
            {
                return Result.Failure<BoltzClaimResponse>("Failed to deserialize chain claim response");
            }

            return Result.Success(new BoltzClaimResponse
            {
                PubNonce = claimResponse.PubNonce,
                PartialSignature = claimResponse.PartialSignature
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting chain claim for swap {SwapId}", swapId);
            return Result.Failure<BoltzClaimResponse>($"Error posting chain claim: {ex.Message}");
        }
    }

    // DTOs for chain claim API
    private class ChainClaimDetailsResponse
    {
        [JsonPropertyName("pubNonce")]
        public string? PubNonce { get; set; }

        [JsonPropertyName("publicKey")]
        public string? PublicKey { get; set; }

        [JsonPropertyName("transactionHash")]
        public string? TransactionHash { get; set; }
    }

    private class ChainClaimApiRequest
    {
        [JsonPropertyName("preimage")]
        public string Preimage { get; set; } = string.Empty;

        [JsonPropertyName("signature")]
        public ChainClaimSignatureDto Signature { get; set; } = new();

        [JsonPropertyName("toSign")]
        public ChainClaimToSignDto ToSign { get; set; } = new();
    }

    private class ChainClaimSignatureDto
    {
        [JsonPropertyName("pubNonce")]
        public string PubNonce { get; set; } = string.Empty;

        [JsonPropertyName("partialSignature")]
        public string PartialSignature { get; set; } = string.Empty;
    }

    private class ChainClaimToSignDto
    {
        [JsonPropertyName("pubNonce")]
        public string PubNonce { get; set; } = string.Empty;

        [JsonPropertyName("transaction")]
        public string Transaction { get; set; } = string.Empty;

        [JsonPropertyName("index")]
        public int Index { get; set; }
    }

    /// <summary>
    /// Request for Liquid→BTC chain swap (POST /swap/chain)
    /// </summary>
    private class CreateChainSwapRequest
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = "L-BTC";

        [JsonPropertyName("to")]
        public string To { get; set; } = "BTC";

        [JsonPropertyName("claimPublicKey")]
        public string ClaimPublicKey { get; set; } = string.Empty;

        [JsonPropertyName("refundPublicKey")]
        public string RefundPublicKey { get; set; } = string.Empty;

        [JsonPropertyName("preimageHash")]
        public string PreimageHash { get; set; } = string.Empty;

        [JsonPropertyName("userLockAmount")]
        public long UserLockAmount { get; set; }
    }

    /// <summary>
    /// Boltz API v2 response for chain swap.
    /// Contains nested claimDetails (BTC side) and lockupDetails (L-BTC side).
    /// </summary>
    private class CreateChainSwapResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("claimDetails")]
        public ChainSwapSideDetails ClaimDetails { get; set; } = new();

        [JsonPropertyName("lockupDetails")]
        public ChainSwapSideDetails LockupDetails { get; set; } = new();
    }

    /// <summary>
    /// Details for one side of a chain swap (either claim or lockup).
    /// </summary>
    private class ChainSwapSideDetails
    {
        [JsonPropertyName("swapTree")]
        public SwapTreeResponse? SwapTree { get; set; }

        [JsonPropertyName("lockupAddress")]
        public string LockupAddress { get; set; } = string.Empty;

        [JsonPropertyName("serverPublicKey")]
        public string? ServerPublicKey { get; set; }

        [JsonPropertyName("timeoutBlockHeight")]
        public long TimeoutBlockHeight { get; set; }

        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("blindingKey")]
        public string? BlindingKey { get; set; }

        [JsonPropertyName("bip21")]
        public string? Bip21 { get; set; }
    }

    /// <summary>
    /// Chain swap fee info response (GET /swap/chain)
    /// </summary>
    private class ChainSwapInfoResponse
    {
        [JsonPropertyName("BTC")]
        public ChainSwapPairGroup? BTC { get; set; }

        [JsonPropertyName("L-BTC")]
        public ChainSwapPairGroup? LBTC { get; set; }
    }

    private class ChainSwapPairGroup
    {
        [JsonPropertyName("BTC")]
        public ChainSwapPairInfo? BTC { get; set; }

        [JsonPropertyName("L-BTC")]
        public ChainSwapPairInfo? LBTC { get; set; }
    }

    private class ChainSwapPairInfo
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;

        [JsonPropertyName("rate")]
        public decimal Rate { get; set; }

        [JsonPropertyName("limits")]
        public ChainSwapLimits Limits { get; set; } = new();

        [JsonPropertyName("fees")]
        public ChainSwapFeesInfo Fees { get; set; } = new();
    }

    private class ChainSwapLimits
    {
        [JsonPropertyName("minimal")]
        public long Minimal { get; set; }

        [JsonPropertyName("maximal")]
        public long Maximal { get; set; }

        [JsonPropertyName("maximalZeroConf")]
        public long MaximalZeroConf { get; set; }
    }

    private class ChainSwapFeesInfo
    {
        [JsonPropertyName("percentage")]
        public decimal Percentage { get; set; }

        [JsonPropertyName("minerFees")]
        public ChainSwapMinerFees MinerFees { get; set; } = new();
    }

    private class ChainSwapMinerFees
    {
        [JsonPropertyName("server")]
        public long Server { get; set; }

        [JsonPropertyName("user")]
        public ChainSwapUserMinerFees User { get; set; } = new();
    }

    private class ChainSwapUserMinerFees
    {
        [JsonPropertyName("claim")]
        public long Claim { get; set; }

        [JsonPropertyName("lockup")]
        public long Lockup { get; set; }
    }

    #endregion
}

