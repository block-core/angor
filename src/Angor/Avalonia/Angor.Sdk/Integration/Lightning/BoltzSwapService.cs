using System.Net.Http.Json;
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

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    public async Task<Result<BoltzSubmarineSwap>> CreateSubmarineSwapAsync(
        string onchainAddress,
        long amountSats,
        string refundPublicKey)
    {
        try
        {
            _logger.LogInformation(
                "Creating submarine swap: {Amount} sats to address {Address}",
                amountSats, onchainAddress);

            // First get pair info to validate amount
            var pairResult = await GetPairInfoAsync();
            if (pairResult.IsFailure)
            {
                return Result.Failure<BoltzSubmarineSwap>(pairResult.Error);
            }

            var pairInfo = pairResult.Value;
            if (amountSats < pairInfo.MinAmount || amountSats > pairInfo.MaxAmount)
            {
                return Result.Failure<BoltzSubmarineSwap>(
                    $"Amount must be between {pairInfo.MinAmount} and {pairInfo.MaxAmount} sats");
            }

            // Boltz API v2 submarine swap request format
            var request = new CreateSubmarineSwapRequest
            {
                From = "BTC",
                To = "L-BTC",
                RefundPublicKey = refundPublicKey,
                InvoiceAmount = amountSats
            };

            var response = await _httpClient.PostAsJsonAsync("/v2/swap/submarine", request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create swap: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltzSubmarineSwap>($"Failed to create swap: {error}");
            }

            var swapResponse = await response.Content.ReadFromJsonAsync<CreateSwapResponse>(_jsonOptions);
            if (swapResponse == null)
            {
                return Result.Failure<BoltzSubmarineSwap>("Failed to deserialize swap response");
            }

            var swap = new BoltzSubmarineSwap
            {
                Id = swapResponse.Id,
                Invoice = swapResponse.Invoice,
                Address = swapResponse.Address,
                ExpectedAmount = swapResponse.ExpectedAmount,
                InvoiceAmount = amountSats,
                TimeoutBlockHeight = swapResponse.TimeoutBlockHeight,
                RedeemScript = swapResponse.RedeemScript,
                Bip21 = swapResponse.Bip21,
                Status = SwapState.Created
            };

            _logger.LogInformation(
                "Submarine swap created: ID={SwapId}, Invoice amount={Amount} sats",
                swap.Id, swap.InvoiceAmount);

            return Result.Success(swap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating submarine swap");
            return Result.Failure<BoltzSubmarineSwap>($"Error creating swap: {ex.Message}");
        }
    }

    public async Task<Result<BoltzSwapStatus>> GetSwapStatusAsync(string swapId)
    {
        try
        {
            _logger.LogDebug("Getting swap status for {SwapId}", swapId);

            // Use v2 API endpoint
            var response = await _httpClient.GetAsync($"/v2/swap/{swapId}");

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

    public async Task<Result<BoltzPairInfo>> GetPairInfoAsync()
    {
        try
        {
            _logger.LogDebug("Getting pair info");

            // Use v2 API endpoint
            var response = await _httpClient.GetAsync("/v2/swap/submarine");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get submarine info: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltzPairInfo>($"Failed to get pair info: {error}");
            }

            var submarineInfo = await response.Content.ReadFromJsonAsync<SubmarineInfoResponse>(_jsonOptions);
            if (submarineInfo?.BTC == null)
            {
                return Result.Failure<BoltzPairInfo>("BTC pair not found in response");
            }

            var btcInfo = submarineInfo.BTC.BTC; // BTC -> BTC (Lightning to on-chain)
            if (btcInfo == null)
            {
                return Result.Failure<BoltzPairInfo>("BTC/BTC pair not found");
            }

            var pairInfo = new BoltzPairInfo
            {
                PairId = "BTC/BTC",
                MinAmount = btcInfo.Limits.Minimal,
                MaxAmount = btcInfo.Limits.Maximal,
                FeePercentage = btcInfo.Fees.Percentage,
                MinerFee = btcInfo.Fees.MinerFees,
                Hash = btcInfo.Hash
            };

            _logger.LogDebug(
                "Pair info: Min={Min}, Max={Max}, Fee={Fee}%",
                pairInfo.MinAmount, pairInfo.MaxAmount, pairInfo.FeePercentage);

            return Result.Success(pairInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pair info");
            return Result.Failure<BoltzPairInfo>($"Error getting pair info: {ex.Message}");
        }
    }

    public async Task<Result<BoltzReverseSwap>> CreateReverseSwapAsync(
        string bolt11Invoice,
        string claimPublicKey)
    {
        try
        {
            _logger.LogInformation("Creating reverse swap for invoice");

            var request = new CreateReverseSwapRequest
            {
                Type = "reversesubmarine",
                PairId = "BTC/BTC",
                OrderSide = "buy",
                ClaimPublicKey = claimPublicKey,
                Invoice = bolt11Invoice
            };

            var response = await _httpClient.PostAsJsonAsync("/createswap", request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create reverse swap: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltzReverseSwap>($"Failed to create reverse swap: {error}");
            }

            var swapResponse = await response.Content.ReadFromJsonAsync<CreateReverseSwapResponse>(_jsonOptions);
            if (swapResponse == null)
            {
                return Result.Failure<BoltzReverseSwap>("Failed to deserialize reverse swap response");
            }

            var swap = new BoltzReverseSwap
            {
                Id = swapResponse.Id,
                Invoice = swapResponse.Invoice,
                LockupAddress = swapResponse.LockupAddress,
                OnchainAmount = swapResponse.OnchainAmount,
                TimeoutBlockHeight = swapResponse.TimeoutBlockHeight,
                RedeemScript = swapResponse.RedeemScript
            };

            _logger.LogInformation("Reverse swap created: ID={SwapId}", swap.Id);

            return Result.Success(swap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reverse swap");
            return Result.Failure<BoltzReverseSwap>($"Error creating reverse swap: {ex.Message}");
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

    // Boltz API v2 submarine info response
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

    #endregion
}

