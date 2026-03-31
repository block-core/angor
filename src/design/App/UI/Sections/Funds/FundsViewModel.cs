using System.Collections.ObjectModel;
using System.Globalization;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Models;
using App.UI.Shared;
using App.UI.Shared.Services;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace App.UI.Sections.Funds;

/// <summary>
/// Seed group (account) containing wallets from the centralized <see cref="IWalletContext"/>.
/// </summary>
public class SeedGroupViewModel
{
    public string GroupName { get; set; } = "";
    public string GroupBalance { get; set; } = "0.00000000";
    public ReadOnlyObservableCollection<WalletInfo>? Wallets { get; set; }
}

/// <summary>
/// Funds ViewModel — connected to Angor.SDK wallet services.
/// Uses <see cref="IWalletContext"/> for wallet state and <see cref="IWalletAppService"/> for wallet operations.
/// </summary>
public partial class FundsViewModel : ReactiveObject
{
    private readonly IWalletAppService _walletAppService;
    private readonly IWalletAccountBalanceService _balanceService;
    private readonly IWalletContext _walletContext;
    private readonly Func<BitcoinNetwork> _getNetwork;
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<FundsViewModel> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>True when wallets exist and populated state should show.</summary>
    [Reactive] private bool hasWallets;

    /// <summary>Sum of all wallet balances</summary>
    [Reactive] private string totalBalance = "0.0000";

    /// <summary>Total invested amount</summary>
    [Reactive] private string totalInvested = "0.0000";

    /// <summary>Bitcoin on-chain balance for stat card</summary>
    [Reactive] private string bitcoinBalance = "0.0000";

    /// <summary>Liquid balance for stat card</summary>
    [Reactive] private string liquidBalance = "0.0000";

    [Reactive] private bool isLoading;

    public ObservableCollection<SeedGroupViewModel> SeedGroups { get; } = new();

    /// <summary>Cached AccountBalanceInfo per wallet for UTXO access.</summary>
    private readonly Dictionary<string, AccountBalanceInfo> _walletBalanceInfos = new();

    /// <summary>Currency symbol from ICurrencyService (e.g. "BTC", "TBTC").</summary>
    public string CurrencySymbol => _currencyService.Symbol;

    /// <summary>True when running on a testnet network (faucet button visible).</summary>
    public bool IsTestnet => _getNetwork() != BitcoinNetwork.Mainnet;

    /// <summary>Default wallet name based on the current network.</summary>
    public string DefaultWalletName => _getNetwork() == BitcoinNetwork.Mainnet ? "Main Wallet" : "Test Wallet";

    public FundsViewModel(
        IWalletAppService walletAppService,
        IWalletAccountBalanceService balanceService,
        IWalletContext walletContext,
        Func<BitcoinNetwork> getNetwork,
        ICurrencyService currencyService,
        ILogger<FundsViewModel> logger,
        IHttpClientFactory httpClientFactory)
    {
        _walletAppService = walletAppService;
        _balanceService = balanceService;
        _walletContext = walletContext;
        _getNetwork = getNetwork;
        _currencyService = currencyService;
        _logger = logger;
        _httpClientFactory = httpClientFactory;

        // Subscribe to wallet context updates to rebuild display state
        _walletContext.WalletsUpdated
            .Subscribe(_ => Dispatcher.UIThread.Post(RebuildSeedGroups));

        RebuildSeedGroups();
    }

    /// <summary>
    /// Get cached AccountBalanceInfo for a wallet (used by WalletDetailModal for real UTXOs).
    /// </summary>
    public AccountBalanceInfo? GetAccountBalanceInfo(string walletId)
    {
        return _walletBalanceInfos.GetValueOrDefault(walletId);
    }

    /// <summary>
    /// Rebuild the <see cref="SeedGroups"/> display collection from <see cref="IWalletContext.Wallets"/>.
    /// This is a pure UI projection — no network calls.
    /// </summary>
    private void RebuildSeedGroups()
    {
        var wallets = _walletContext.Wallets;

        if (wallets.Count == 0)
        {
            ClearToEmpty();
            return;
        }

        long totalSats = 0;
        long btcSats = 0;

        foreach (var wallet in wallets)
        {
            totalSats += wallet.TotalBalanceSats;
            btcSats += wallet.TotalBalanceSats;
        }

        double totalBtc = (double)totalSats.ToUnitBtc();
        double btcBtc = (double)btcSats.ToUnitBtc();

        var group = new SeedGroupViewModel
        {
            GroupName = "Wallets",
            GroupBalance = totalBtc.ToString("F4", CultureInfo.InvariantCulture),
            Wallets = _walletContext.Wallets,
        };

        SeedGroups.Clear();
        SeedGroups.Add(group);

        TotalBalance = totalBtc.ToString("F4", CultureInfo.InvariantCulture);
        BitcoinBalance = btcBtc.ToString("F4", CultureInfo.InvariantCulture);
        HasWallets = true;

        _logger.LogInformation("SeedGroups rebuilt — TotalBalance: {TotalBalance} ({TotalSats} sats), {Count} wallet(s)",
            TotalBalance, totalSats, wallets.Count);
    }

    /// <summary>
    /// Reload wallets from SDK (delegates to <see cref="IWalletContext.ReloadAsync"/>).
    /// Called from code-behind on view re-attach and after wallet create/import.
    /// </summary>
    public async Task ReloadWalletsAsync()
    {
        IsLoading = true;
        try
        {
            await _walletContext.ReloadAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Create a new wallet using the SDK.
    /// Generates random seed words, creates wallet with encryption key.
    /// </summary>
    public async Task<(bool Success, string? SeedWords)> CreateWalletAsync(string walletName, string encryptionKey)
    {
        _logger.LogInformation("Creating new wallet '{WalletName}' (generating random seed words)", walletName);
        var seedWords = _walletAppService.GenerateRandomSeedwords();

        var result = await _walletAppService.CreateWallet(
            walletName,
            seedWords,
            CSharpFunctionalExtensions.Maybe<string>.None,
            encryptionKey,
            _getNetwork());

        if (result.IsSuccess)
        {
            _logger.LogInformation("Wallet '{WalletName}' created successfully (WalletId: {WalletId})", walletName, result.Value);
            await _walletContext.ReloadAsync();
            return (true, seedWords);
        }

        _logger.LogError("Failed to create wallet '{WalletName}': {Error}", walletName, result.Error);
        return (false, null);
    }

    /// <summary>
    /// Import an existing wallet from user-provided seed words.
    /// </summary>
    public async Task<bool> ImportWalletAsync(string walletName, string seedWords, string encryptionKey)
    {
        _logger.LogInformation("Importing wallet '{WalletName}' from seed words", walletName);
        var result = await _walletAppService.CreateWallet(
            walletName,
            seedWords,
            CSharpFunctionalExtensions.Maybe<string>.None,
            encryptionKey,
            _getNetwork());

        if (result.IsSuccess)
        {
            _logger.LogInformation("Wallet '{WalletName}' imported successfully (WalletId: {WalletId})", walletName, result.Value);
            await _walletContext.ReloadAsync();
            return true;
        }

        _logger.LogError("Failed to import wallet '{WalletName}': {Error}", walletName, result.Error);
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
            var sats = ((decimal)amountBtc).ToUnitSatoshi();
            var id = new WalletId(walletId);
            var result = await _walletAppService.SendAmount(
                id,
                new Amount(sats),
                new Address(destinationAddress),
                new DomainFeeRate(feeRateSatsPerVByte));

            if (result.IsSuccess)
            {
                // Refresh the sending wallet's balance from the indexer
                await _walletContext.RefreshBalanceAsync(id);
                return (true, result.Value.Value);
            }

            _logger.LogError("SendAmount failed for wallet {WalletId} to address '{Address}' amount {Sats} sats feeRate {FeeRate}: {Error}", walletId, destinationAddress, sats, feeRateSatsPerVByte, result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendAsync threw an exception for wallet {WalletId}", walletId);
        }

        return (false, null);
    }

    /// <summary>
    /// Generate random BIP-39 seed words via the SDK.
    /// </summary>
    public string GenerateSeedWords()
    {
        _logger.LogInformation("Generating random BIP-39 seed words");
        var words = _walletAppService.GenerateRandomSeedwords();
        _logger.LogInformation("Seed words generated ({WordCount} words)", words.Split(' ').Length);
        return words;
    }

    /// <summary>
    /// Get a receive address for the specified wallet.
    /// </summary>
    public async Task<string?> GetReceiveAddressAsync(string walletId)
    {
        _logger.LogInformation("Getting next receive address for wallet {WalletId}", walletId);
        var result = await _walletAppService.GetNextReceiveAddress(new WalletId(walletId));
        if (result.IsSuccess)
        {
            _logger.LogInformation("Receive address for wallet {WalletId}: {Address}", walletId, result.Value.Value);
            return result.Value.Value;
        }

        _logger.LogWarning("Failed to get receive address for wallet {WalletId}: {Error}", walletId, result.Error);
        return null;
    }

    /// <summary>
    /// Refresh balance for a specific wallet.
    /// </summary>
    public async Task RefreshBalanceAsync(string walletId)
    {
        await _walletContext.RefreshBalanceAsync(new WalletId(walletId));
    }

    /// <summary>
    /// Refresh all wallet balances.
    /// </summary>
    public async Task RefreshAllBalancesAsync()
    {
        await _walletContext.RefreshAllBalancesAsync();
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
            var id = new WalletId(walletId);

            // Refresh balance info first to get UTXOs and address
            var refreshResult = await _walletAppService.RefreshAndGetAccountBalanceInfo(id);
            if (refreshResult.IsFailure)
            {
                _logger.LogWarning("Failed to refresh balance for wallet {WalletId}: {Error}", walletId, refreshResult.Error);
                return (false, "Cannot get wallet balance");
            }

            var balanceInfo = refreshResult.Value;
            _walletBalanceInfos[walletId] = balanceInfo;

            // Guard: don't request coins if balance > 100 BTC
            long totalSats = balanceInfo.TotalBalance + balanceInfo.TotalUnconfirmedBalance;
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
            var response = await httpClient.GetAsync($"https://faucettmp.angor.io/api/faucet/send/{address}/2");

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Faucet HTTP request failed: {StatusCode} {Reason} {Body}", response.StatusCode, response.ReasonPhrase, body);
                return (false, $"Faucet request failed: {response.ReasonPhrase} - {body}");
            }

            _logger.LogInformation("Faucet request succeeded for wallet {WalletId}, refreshing balance", walletId);
            await _walletContext.RefreshBalanceAsync(id);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Faucet request threw an exception for wallet {WalletId}", walletId);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Refresh the UTXO cache for a specific wallet (used before opening WalletDetailModal).
    /// </summary>
    public async Task RefreshUtxoCacheAsync(string walletId)
    {
        var result = await _walletAppService.RefreshAndGetAccountBalanceInfo(new WalletId(walletId));
        if (result.IsSuccess)
        {
            _walletBalanceInfos[walletId] = result.Value;
        }
    }

    /// <summary>
    /// Clear all wallet data and show empty state.
    /// </summary>
    public void ClearToEmpty()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _walletBalanceInfos.Clear();
            SeedGroups.Clear();
            TotalBalance = "0.0000";
            TotalInvested = "0.0000";
            BitcoinBalance = "0.0000";
            LiquidBalance = "0.0000";
            HasWallets = false;
        });
    }

}
