using System.Net.Http.Json;
using System.Text.Json;
using Angor.Sdk.Integration.Lightning.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Integration.Lightning;

/// <summary>
/// Implementation of Bolt Lightning API service
/// </summary>
public class BoltService : IBoltService
{
    private readonly HttpClient _httpClient;
    private readonly BoltConfiguration _configuration;
    private readonly ILogger<BoltService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public BoltService(
        HttpClient httpClient, 
        BoltConfiguration configuration,
        ILogger<BoltService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_configuration.ApiKey}");
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    public async Task<Result<BoltWallet>> CreateWalletAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Creating Bolt wallet for user {UserId}", userId);

            var request = new { user_id = userId };
            var response = await _httpClient.PostAsJsonAsync("/v1/wallets", request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create wallet: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltWallet>($"Failed to create wallet: {response.StatusCode}");
            }

            var wallet = await response.Content.ReadFromJsonAsync<BoltWallet>(_jsonOptions);
            if (wallet == null)
            {
                return Result.Failure<BoltWallet>("Failed to deserialize wallet response");
            }

            _logger.LogInformation("Successfully created wallet {WalletId} for user {UserId}", wallet.Id, userId);
            return Result.Success(wallet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating wallet for user {UserId}", userId);
            return Result.Failure<BoltWallet>($"Error creating wallet: {ex.Message}");
        }
    }

    public async Task<Result<BoltWallet>> GetWalletAsync(string walletId)
    {
        try
        {
            _logger.LogDebug("Getting wallet {WalletId}", walletId);

            var response = await _httpClient.GetAsync($"/v1/wallets/{walletId}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get wallet: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltWallet>($"Failed to get wallet: {response.StatusCode}");
            }

            var wallet = await response.Content.ReadFromJsonAsync<BoltWallet>(_jsonOptions);
            if (wallet == null)
            {
                return Result.Failure<BoltWallet>("Failed to deserialize wallet response");
            }

            return Result.Success(wallet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallet {WalletId}", walletId);
            return Result.Failure<BoltWallet>($"Error getting wallet: {ex.Message}");
        }
    }

    public async Task<Result<long>> GetWalletBalanceAsync(string walletId)
    {
        try
        {
            var walletResult = await GetWalletAsync(walletId);
            return walletResult.IsSuccess 
                ? Result.Success(walletResult.Value.BalanceSats) 
                : Result.Failure<long>(walletResult.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallet balance for {WalletId}", walletId);
            return Result.Failure<long>($"Error getting wallet balance: {ex.Message}");
        }
    }

    public async Task<Result<BoltInvoice>> CreateInvoiceAsync(string walletId, long amountSats, string memo)
    {
        try
        {
            _logger.LogInformation("Creating invoice for wallet {WalletId}, amount: {Amount} sats", walletId, amountSats);

            var request = new 
            { 
                amount_sats = amountSats, 
                memo = memo
            };
            
            var response = await _httpClient.PostAsJsonAsync($"/v1/wallets/{walletId}/invoices", request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create invoice: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltInvoice>($"Failed to create invoice: {response.StatusCode}");
            }

            var invoice = await response.Content.ReadFromJsonAsync<BoltInvoice>(_jsonOptions);
            if (invoice == null)
            {
                return Result.Failure<BoltInvoice>("Failed to deserialize invoice response");
            }

            _logger.LogInformation("Successfully created invoice {InvoiceId} for {Amount} sats", invoice.Id, amountSats);
            return Result.Success(invoice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating invoice for wallet {WalletId}", walletId);
            return Result.Failure<BoltInvoice>($"Error creating invoice: {ex.Message}");
        }
    }

    public async Task<Result<BoltInvoice>> GetInvoiceAsync(string invoiceId)
    {
        try
        {
            _logger.LogDebug("Getting invoice {InvoiceId}", invoiceId);

            var response = await _httpClient.GetAsync($"/v1/invoices/{invoiceId}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get invoice: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltInvoice>($"Failed to get invoice: {response.StatusCode}");
            }

            var invoice = await response.Content.ReadFromJsonAsync<BoltInvoice>(_jsonOptions);
            if (invoice == null)
            {
                return Result.Failure<BoltInvoice>("Failed to deserialize invoice response");
            }

            return Result.Success(invoice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting invoice {InvoiceId}", invoiceId);
            return Result.Failure<BoltInvoice>($"Error getting invoice: {ex.Message}");
        }
    }

    public async Task<Result<BoltPaymentStatus>> GetPaymentStatusAsync(string invoiceId)
    {
        try
        {
            var invoiceResult = await GetInvoiceAsync(invoiceId);
            return invoiceResult.IsSuccess 
                ? Result.Success(invoiceResult.Value.Status) 
                : Result.Failure<BoltPaymentStatus>(invoiceResult.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment status for invoice {InvoiceId}", invoiceId);
            return Result.Failure<BoltPaymentStatus>($"Error getting payment status: {ex.Message}");
        }
    }

    public async Task<Result<BoltPayment>> PayInvoiceAsync(string walletId, string bolt11Invoice)
    {
        try
        {
            _logger.LogInformation("Paying invoice from wallet {WalletId}", walletId);

            var request = new { bolt11 = bolt11Invoice };
            var response = await _httpClient.PostAsJsonAsync($"/v1/wallets/{walletId}/payments", request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to pay invoice: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<BoltPayment>($"Failed to pay invoice: {response.StatusCode}");
            }

            var payment = await response.Content.ReadFromJsonAsync<BoltPayment>(_jsonOptions);
            if (payment == null)
            {
                return Result.Failure<BoltPayment>("Failed to deserialize payment response");
            }

            _logger.LogInformation("Successfully initiated payment {PaymentId}", payment.Id);
            return Result.Success(payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error paying invoice from wallet {WalletId}", walletId);
            return Result.Failure<BoltPayment>($"Error paying invoice: {ex.Message}");
        }
    }

    public async Task<Result<List<BoltInvoice>>> ListInvoicesAsync(string walletId, int limit = 100)
    {
        try
        {
            _logger.LogDebug("Listing invoices for wallet {WalletId}", walletId);

            var response = await _httpClient.GetAsync($"/v1/wallets/{walletId}/invoices?limit={limit}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to list invoices: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<List<BoltInvoice>>($"Failed to list invoices: {response.StatusCode}");
            }

            var invoices = await response.Content.ReadFromJsonAsync<List<BoltInvoice>>(_jsonOptions);
            if (invoices == null)
            {
                return Result.Failure<List<BoltInvoice>>("Failed to deserialize invoices response");
            }

            return Result.Success(invoices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing invoices for wallet {WalletId}", walletId);
            return Result.Failure<List<BoltInvoice>>($"Error listing invoices: {ex.Message}");
        }
    }

    public async Task<Result<string>> GetSwapAddressAsync(string walletId, long amountSats)
    {
        try
        {
            _logger.LogInformation("Getting swap address for wallet {WalletId}, amount: {Amount} sats", walletId, amountSats);

            var request = new { amount_sats = amountSats };
            var response = await _httpClient.PostAsJsonAsync($"/v1/wallets/{walletId}/swap", request, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get swap address: {StatusCode} - {Error}", response.StatusCode, error);
                return Result.Failure<string>($"Failed to get swap address: {response.StatusCode}");
            }

            var swapResponse = await response.Content.ReadFromJsonAsync<SwapAddressResponse>(_jsonOptions);
            if (swapResponse == null || string.IsNullOrEmpty(swapResponse.Address))
            {
                return Result.Failure<string>("Failed to get swap address from response");
            }

            _logger.LogInformation("Successfully got swap address {Address} for wallet {WalletId}", swapResponse.Address, walletId);
            return Result.Success(swapResponse.Address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting swap address for wallet {WalletId}", walletId);
            return Result.Failure<string>($"Error getting swap address: {ex.Message}");
        }
    }

    private class SwapAddressResponse
    {
        public string Address { get; set; } = string.Empty;
    }
}

