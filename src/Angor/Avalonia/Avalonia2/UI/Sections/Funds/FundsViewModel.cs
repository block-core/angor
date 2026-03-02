using System.Collections.ObjectModel;
using System.Globalization;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Avalonia2.Composition;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Sections.Funds;

/// <summary>
/// Individual wallet item within a seed group.
/// </summary>
public class WalletItemViewModel
{
    public string Name { get; set; } = "";
    public string Balance { get; set; } = "0.00000000 BTC";
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

    public FundsViewModel()
    {
        _walletAppService = ServiceLocator.WalletApp;
        _balanceService = ServiceLocator.BalanceService;

        // Load wallets from SDK on construction
        _ = LoadWalletsFromSdkAsync();
    }

    /// <summary>
    /// Load wallet data from the SDK.
    /// Retrieves all wallet metadatas and their balances.
    /// </summary>
    public async Task LoadWalletsFromSdkAsync()
    {
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
            double totalBal = 0;
            double btcBal = 0;

            var group = new SeedGroupViewModel
            {
                GroupName = "Wallets",
                Wallets = new ObservableCollection<WalletItemViewModel>()
            };

            foreach (var meta in metadatas)
            {
                var walletId = meta.Id;
                var balanceResult = await _walletAppService.GetBalance(walletId);
                double balanceValue = 0;

                if (balanceResult.IsSuccess)
                {
                    // Balance is in satoshis, convert to BTC
                    balanceValue = balanceResult.Value.Sats / 100_000_000.0;
                }

                totalBal += balanceValue;
                btcBal += balanceValue;

                group.Wallets.Add(new WalletItemViewModel
                {
                    Name = meta.Name,
                    Balance = $"{balanceValue:F8} BTC",
                    WalletType = "On-Chain",
                    Label = "",
                    IconType = "bitcoin",
                    WalletId = walletId.Value
                });
            }

            group.GroupBalance = totalBal.ToString("F4", CultureInfo.InvariantCulture);
            SeedGroups.Add(group);

            TotalBalance = totalBal.ToString("F4", CultureInfo.InvariantCulture);
            BitcoinBalance = btcBal.ToString("F4", CultureInfo.InvariantCulture);
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
            Angor.Sdk.Wallet.Domain.BitcoinNetwork.Testnet);

        if (result.IsSuccess)
        {
            // Refresh wallet list
            await LoadWalletsFromSdkAsync();
            return (true, seedWords);
        }

        return (false, null);
    }

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
    /// Get test coins for a wallet (testnet/signet only).
    /// </summary>
    public async Task<bool> GetTestCoinsAsync(string walletId)
    {
        var result = await _walletAppService.GetTestCoins(new WalletId(walletId));
        if (result.IsSuccess)
        {
            await LoadWalletsFromSdkAsync();
        }
        return result.IsSuccess;
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

    /// <summary>
    /// Add a new wallet group (called from the create wallet modal).
    /// This is the UI-only fallback; prefer CreateWalletAsync for SDK integration.
    /// </summary>
    public void AddWalletGroup(string groupName, string walletType)
    {
        var group = new SeedGroupViewModel
        {
            GroupName = groupName,
            GroupBalance = "0.0000",
            Wallets = new ObservableCollection<WalletItemViewModel>
            {
                new()
                {
                    Name = "Bitcoin Wallet",
                    Balance = "0.00000000 BTC",
                    WalletType = "On-Chain",
                    Label = "",
                    IconType = "bitcoin"
                }
            }
        };

        SeedGroups.Add(group);
        HasWallets = true;
    }
}
