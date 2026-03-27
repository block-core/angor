using System.Collections.ObjectModel;
using System.Globalization;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Models;
using App.UI.Shell;
using App.UI.Shared;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace App.UI.Sections.Funds;

/// <summary>
/// Individual wallet item within a seed group.
/// </summary>
public class WalletItemViewModel
{
    public string Name { get; set; } = "";
    public string Balance { get; set; } = "0.00000000";
    public string WalletType { get; set; } = "On-Chain";
    public string Label { get; set; } = "";
    /// <summary>bitcoin, lightning, liquid</summary>
    public string IconType { get; set; } = "bitcoin";
    /// <summary>SDK WalletId for operations</summary>
    public string WalletId { get; set; } = "";
}

/// <summary>
/// Seed group (account) containing multiple wallets.
/// </summary>
public class SeedGroupViewModel
{
    public string GroupName { get; set; } = "";
    public string GroupBalance { get; set; } = "0.00000000";
    public ObservableCollection<WalletItemViewModel> Wallets { get; set; } = new();
}

/// <summary>
/// Funds ViewModel — connected to Angor.SDK wallet services.
/// Uses IWalletAppService for wallet creation, balance, and address operations.
/// </summary>
public partial class FundsViewModel : ReactiveObject
{
    private readonly IWalletAppService _walletAppService;
    private readonly IWalletAccountBalanceService _balanceService;
    private readonly Func<BitcoinNetwork> _getNetwork;
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<FundsViewModel> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>True when wallets exist and populated state should show.</summary>
    [Reactive] private bool hasWallets;

    /// <summary>Sum of all wallet balances</summary>
    public string TotalBalance { get; private set; } = "0.0000";

    /// <summary>Total invested amount</summary>
    public string TotalInvested { get; private set; } = "0.0000";

    /// <summary>Bitcoin on-chain balance for stat card</summary>
    public string BitcoinBalance { get; private set; } = "0.0000";

    /// <summary>Liquid balance for stat card</summary>
    public string LiquidBalance { get; private set; } = "0.0000";

    [Reactive] private bool isLoading;

    public ObservableCollection<SeedGroupViewModel> SeedGroups { get; } = new();

    /// <summary>Cached AccountBalanceInfo per wallet for UTXO access.</summary>
    private readonly Dictionary<string, AccountBalanceInfo> _walletBalanceInfos = new();

    /// <summary>Guard against concurrent LoadWalletsFromSdkAsync calls.</summary>
    private bool _isLoadingWallets;

    /// <summary>Currency symbol from ICurrencyService (e.g. "BTC", "TBTC").</summary>
    public string CurrencySymbol => _currencyService.Symbol;

    /// <summary>True when running on a testnet network (faucet button visible).</summary>
    public bool IsTestnet => _getNetwork() != BitcoinNetwork.Mainnet;

    public FundsViewModel(
        IWalletAppService walletAppService,
        IWalletAccountBalanceService balanceService,
        Func<BitcoinNetwork> getNetwork,
        ICurrencyService currencyService,
        ILogger<FundsViewModel> logger,
        IHttpClientFactory httpClientFactory)
    {
        _walletAppService = walletAppService;
        _balanceService = balanceService;
        _getNetwork = getNetwork;
        _currencyService = currencyService;
        _logger = logger;
        _httpClientFactory = httpClientFactory;

        _ = LoadWalletsFromSdkAsync();
    }

    /// <summary>
    /// Get cached AccountBalanceInfo for a wallet (used by WalletDetailModal for real UTXOs).
    /// </summary>
    public AccountBalanceInfo? GetAccountBalanceInfo(string walletId)
    {
        return _walletBalanceInfos.GetValueOrDefault(walletId);
    }

    /// <summary>
    /// Load wallet data from the SDK.
    /// Uses RefreshAndGetAccountBalanceInfo to get UTXO-based balances.
    /// </summary>
    public async Task LoadWalletsFromSdkAsync()
    {
        if (_isLoadingWallets) return;
        _isLoadingWallets = true;
        IsLoading = true;

        try
        {
            var metadatasResult = await _walletAppService.GetMetadatas();
            if (metadatasResult.IsFailure)
            {
                ClearToEmpty();
                return;
            }

            var metadatas = metadatasResult.Value.ToList();
            if (metadatas.Count == 0)
            {
                ClearToEmpty();
                return;
            }

            SeedGroups.Clear();
            _walletBalanceInfos.Clear();
            long totalSats = 0;
            long btcSats = 0;

            var group = new SeedGroupViewModel
            {
                GroupName = "Wallets",
                Wallets = new ObservableCollection<WalletItemViewModel>()
            };

            foreach (var meta in metadatas)
            {
                var walletId = meta.Id;
                long balanceSats = 0;

                // Use AccountBalanceInfo for UTXO-based balance
                var balanceInfoResult = await _walletAppService.RefreshAndGetAccountBalanceInfo(walletId);
                if (balanceInfoResult.IsSuccess)
                {
                    var info = balanceInfoResult.Value;
                    _walletBalanceInfos[walletId.Value] = info;
                    balanceSats = info.TotalBalance + info.TotalUnconfirmedBalance + info.TotalBalanceReserved;
                }

                totalSats += balanceSats;
                btcSats += balanceSats;

                double balanceBtc = balanceSats / 100_000_000.0;

                group.Wallets.Add(new WalletItemViewModel
                {
                    Name = meta.Name,
                    Balance = $"{balanceBtc:F8} {_currencyService.Symbol}",
                    WalletType = "On-Chain",
                    Label = "",
                    IconType = "bitcoin",
                    WalletId = walletId.Value
                });
            }

            double totalBtc = totalSats / 100_000_000.0;
            double btcBtc = btcSats / 100_000_000.0;

            group.GroupBalance = totalBtc.ToString("F4", CultureInfo.InvariantCulture);
            SeedGroups.Add(group);

            TotalBalance = totalBtc.ToString("F4", CultureInfo.InvariantCulture);
            BitcoinBalance = btcBtc.ToString("F4", CultureInfo.InvariantCulture);
            HasWallets = true;

            this.RaisePropertyChanged(nameof(TotalBalance));
            this.RaisePropertyChanged(nameof(BitcoinBalance));
        }
        catch
        {
            ClearToEmpty();
        }
        finally
        {
            IsLoading = false;
            _isLoadingWallets = false;
        }
    }

    /// <summary>
    /// Create a new wallet using the SDK.
    /// Generates random seed words, creates wallet with encryption key.
    /// </summary>
    public async Task<(bool Success, string? SeedWords)> CreateWalletAsync(string walletName, string encryptionKey)
    {
        var seedWords = _walletAppService.GenerateRandomSeedwords();

        var result = await _walletAppService.CreateWallet(
            walletName,
            seedWords,
            CSharpFunctionalExtensions.Maybe<string>.None,
            encryptionKey,
            _getNetwork());

        if (result.IsSuccess)
        {
            await LoadWalletsFromSdkAsync();
            return (true, seedWords);
        }

        return (false, null);
    }

    /// <summary>
    /// Import an existing wallet from user-provided seed words.
    /// </summary>
    public async Task<bool> ImportWalletAsync(string walletName, string seedWords, string encryptionKey)
    {
        var result = await _walletAppService.CreateWallet(
            walletName,
            seedWords,
            CSharpFunctionalExtensions.Maybe<string>.None,
            encryptionKey,
            _getNetwork());

        if (result.IsSuccess)
        {
            await LoadWalletsFromSdkAsync();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Send funds from a wallet to a destination address.
    /// Returns the transaction ID on success.
    /// </summary>
    public async Task<(bool Success, string? TxId)> SendAsync(string walletId, string destinationAddress, double amountBtc, long feeRateSatsPerVByte)
    {
        try
        {
            var sats = (long)(amountBtc * 100_000_000);
            var result = await _walletAppService.SendAmount(
                new WalletId(walletId),
                new Amount(sats),
                new Address(destinationAddress),
                new DomainFeeRate(feeRateSatsPerVByte));

            if (result.IsSuccess)
            {
                await LoadWalletsFromSdkAsync();
                return (true, result.Value.Value);
            }
        }
        catch { }

        return (false, null);
    }

    /// <summary>
    /// Generate random BIP-39 seed words via the SDK.
    /// </summary>
    public string GenerateSeedWords() => _walletAppService.GenerateRandomSeedwords();

    /// <summary>
    /// Get a receive address for the specified wallet.
    /// </summary>
    public async Task<string?> GetReceiveAddressAsync(string walletId)
    {
        var result = await _walletAppService.GetNextReceiveAddress(new WalletId(walletId));
        return result.IsSuccess ? result.Value.Value : null;
    }

    /// <summary>
    /// Refresh balance for a specific wallet.
    /// </summary>
    public async Task RefreshBalanceAsync(string walletId)
    {
        await _balanceService.RefreshAccountBalanceInfoAsync(new WalletId(walletId));
        await LoadWalletsFromSdkAsync();
    }

    /// <summary>
    /// Refresh all wallet balances.
    /// </summary>
    public async Task RefreshAllBalancesAsync()
    {
        await LoadWalletsFromSdkAsync();
    }

    /// <summary>
    /// Get test coins for a wallet (testnet/signet only).
    /// Bypasses the SDK's GetTestCoins to avoid needing wallet encryption key.
    /// Uses cached AccountBalanceInfo for balance check and address, then calls the faucet HTTP API directly.
    /// Returns (success, errorMessage) so the caller can display the error.
    /// </summary>
    public async Task<(bool Success, string? Error)> GetTestCoinsAsync(string walletId)
    {
        _logger.LogInformation("Requesting testnet coins for wallet {WalletId}", walletId);

        try
        {
            // Use cached balance info instead of SDK's GetBalance (which requires sensitive data)
            if (!_walletBalanceInfos.TryGetValue(walletId, out var balanceInfo))
            {
                _logger.LogWarning("No cached balance info for wallet {WalletId}, refreshing first", walletId);

                var refreshResult = await _walletAppService.RefreshAndGetAccountBalanceInfo(new WalletId(walletId));
                if (refreshResult.IsFailure)
                {
                    _logger.LogWarning("Failed to refresh balance for wallet {WalletId}: {Error}", walletId, refreshResult.Error);
                    return (false, "Cannot get wallet balance");
                }

                balanceInfo = refreshResult.Value;
                _walletBalanceInfos[walletId] = balanceInfo;
            }

            // Guard: don't request coins if balance > 100 BTC
            long totalSats = balanceInfo.TotalBalance + balanceInfo.TotalUnconfirmedBalance + balanceInfo.TotalBalanceReserved;
            if (totalSats > 100_00000000)
            {
                _logger.LogInformation("Wallet {WalletId} already has {Sats} sats, exceeds 100 BTC limit", walletId, totalSats);
                return (false, "You already have too much test coins!");
            }

            // Get receive address from cached AccountInfo (no sensitive data needed)
            string? address = balanceInfo.AccountInfo.GetNextReceiveAddress();
            if (string.IsNullOrEmpty(address))
            {
                _logger.LogWarning("No receive address available for wallet {WalletId}", walletId);
                return (false, "Cannot get receive address");
            }

            // Call faucet API directly
            _logger.LogInformation("Calling faucet API for address {Address}", address);
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync($"https://faucettmp.angor.io/api/faucet/send/{address}/10");

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Faucet HTTP request failed: {StatusCode} {Reason} {Body}", response.StatusCode, response.ReasonPhrase, body);
                return (false, $"Faucet request failed: {response.ReasonPhrase} - {body}");
            }

            _logger.LogInformation("Faucet request succeeded for wallet {WalletId}, reloading balances", walletId);
            await LoadWalletsFromSdkAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Faucet request threw an exception for wallet {WalletId}", walletId);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Clear all wallet data and show empty state.
    /// </summary>
    public void ClearToEmpty()
    {
        SeedGroups.Clear();
        TotalBalance = "0.0000";
        TotalInvested = "0.0000";
        BitcoinBalance = "0.0000";
        LiquidBalance = "0.0000";
        HasWallets = false;
        this.RaisePropertyChanged(nameof(TotalBalance));
        this.RaisePropertyChanged(nameof(TotalInvested));
        this.RaisePropertyChanged(nameof(BitcoinBalance));
        this.RaisePropertyChanged(nameof(LiquidBalance));
    }

}
