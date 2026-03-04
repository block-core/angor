<<<<<<<< HEAD:src/Angor/Shared/Integration/Lightning/BoltzSwapService.cs
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Angor.Shared.Integration.Lightning.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Integration.Lightning;

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

            var normalizedClaimPubKey = claimPublicKey.Trim().ToLowerInvariant();

            _logger.LogDebug(
                "Claim public key: {Key} ({Len} chars)",
                normalizedClaimPubKey, normalizedClaimPubKey.Length);

            if (normalizedClaimPubKey.Length != 66 && normalizedClaimPubKey.Length != 64)
            {
                _logger.LogError(
                    "Invalid claim public key length: {Length} chars (expected 66 or 64). Key: {Key}",
                    normalizedClaimPubKey.Length, normalizedClaimPubKey);
                return Result.Failure<BoltzSubmarineSwap>(
                    $"Invalid claim public key: expected 66 or 64 hex chars, got {normalizedClaimPubKey.Length}");
            }

            var preimage = GeneratePreimage();
            var preimageHash = ComputePreimageHash(preimage);

            _logger.LogDebug("Generated preimage hash: {Hash}", preimageHash);

            var request = new CreateReverseSubmarineSwapRequest
            {
                From = "BTC",
                To = "BTC",
                ClaimPublicKey = normalizedClaimPubKey,
                PreimageHash = preimageHash,
                InvoiceAmount = amountSats,
                Address = onchainAddress
            };

            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            _logger.LogInformation(
                "Sending reverse swap request (automatic claim mode) - " +
                "PreimageHash: {PreimageHash} ({HashLen} chars), Amount: {Amount}, Address: {Address}",
                preimageHash, preimageHash.Length,
                amountSats, onchainAddress);
            _logger.LogInformation("Request JSON: {Json}", requestJson);

            var content = new StringContent(requestJson, System.Text.Encoding.UTF8);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
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
                RedeemScript = string.Empty,
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

    private static string GeneratePreimage()
    {
        var preimage = new byte[32];
        RandomNumberGenerator.Fill(preimage);
        return Convert.ToHexString(preimage).ToLowerInvariant();
    }

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

    public async Task<Result<string>> BroadcastTransactionAsync(string transactionHex)
    {
        try
        {
            _logger.LogInformation("Broadcasting transaction via Boltz API");

            var request = new BroadcastRequest { Hex = transactionHex };
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

    public async Task<Result<BoltzSwapFees>> GetReverseSwapFeesAsync()
    {
        try
        {
            _logger.LogDebug("Fetching reverse swap fee information from Boltz");

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

    public async Task<Result<long>> CalculateInvoiceAmountAsync(long desiredOnChainAmount)
    {
        var feesResult = await GetReverseSwapFeesAsync();
        if (feesResult.IsFailure)
        {
            return Result.Failure<long>(feesResult.Error);
        }

        var fees = feesResult.Value;
        var invoiceAmount = fees.CalculateInvoiceAmount(desiredOnChainAmount);

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
}

========
// Moved to Angor.Shared.Integration.Lightning
>>>>>>>> e7fcac64 (Refactor Boltz integration: move DTOs to Angor.Shared.Integration.Lightning and implement XunitLogger for test output):src/Angor/Avalonia/Angor.Sdk/Integration/Lightning/BoltzSwapService.cs
