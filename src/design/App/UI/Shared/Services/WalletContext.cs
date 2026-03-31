using System.Collections.ObjectModel;
using System.Reactive.Subjects;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using App.UI.Shared;
using App.UI.Shell;
using Microsoft.Extensions.Logging;

namespace App.UI.Shared.Services;

/// <summary>
/// Singleton implementation of <see cref="IWalletContext"/>.
/// Single source of truth for wallet metadata + balances across the entire app.
/// </summary>
public class WalletContext : IWalletContext
{
    private readonly IWalletAppService _walletAppService;
    private readonly ICurrencyService _currencyService;
    private readonly PrototypeSettings _settings;
    private readonly ILogger<WalletContext> _logger;

    private readonly ObservableCollection<WalletInfo> _wallets = new();
    private readonly Subject<Unit> _walletsUpdated = new();
    private bool _isReloading;

    public ReadOnlyObservableCollection<WalletInfo> Wallets { get; }

    private WalletInfo? _selectedWallet;
    public WalletInfo? SelectedWallet
    {
        get => _selectedWallet;
        set
        {
            if (_selectedWallet == value) return;

            // Deselect previous
            if (_selectedWallet != null) _selectedWallet.IsSelected = false;

            _selectedWallet = value;

            // Select new
            if (_selectedWallet != null) _selectedWallet.IsSelected = true;

            // Persist selection
            _settings.SelectedWalletId = value?.Id.Value;
        }
    }

    public IObservable<Unit> WalletsUpdated => _walletsUpdated;

    public WalletContext(
        IWalletAppService walletAppService,
        ICurrencyService currencyService,
        PrototypeSettings settings,
        ILogger<WalletContext> logger)
    {
        _walletAppService = walletAppService;
        _currencyService = currencyService;
        _settings = settings;
        _logger = logger;
        Wallets = new ReadOnlyObservableCollection<WalletInfo>(_wallets);
    }

    public async Task ReloadAsync()
    {
        if (_isReloading) return;
        _isReloading = true;

        try
        {
            _logger.LogInformation("WalletContext.ReloadAsync starting...");

            // GetMetadatas() internally calls GetAllAccountBalancesAsync() — single LiteDB scan
            var metadatasResult = await _walletAppService.GetMetadatas();
            if (metadatasResult.IsFailure)
            {
                _logger.LogWarning("GetMetadatas failed: {Error}", metadatasResult.Error);
                _wallets.Clear();
                SelectedWallet = null;
                _walletsUpdated.OnNext(Unit.Default);
                return;
            }

            var metadatas = metadatasResult.Value.ToList();
            var previousSelectedId = _selectedWallet?.Id.Value ?? _settings.SelectedWalletId;
            _wallets.Clear();

            foreach (var meta in metadatas)
            {
                var wallet = new WalletInfo(meta.Id, meta.Name, _currencyService.Symbol);

                // GetAccountBalanceInfo is a single LiteDB read per wallet (no network)
                var balanceResult = await _walletAppService.GetAccountBalanceInfo(meta.Id);
                if (balanceResult.IsSuccess)
                {
                    wallet.TotalBalanceSats = balanceResult.Value.TotalBalance;
                    wallet.UnconfirmedBalanceSats = balanceResult.Value.TotalUnconfirmedBalance;
                    wallet.ReservedBalanceSats = balanceResult.Value.TotalBalanceReserved;
                }

                _wallets.Add(wallet);
            }

            // Restore selection
            WalletInfo? match = null;
            if (previousSelectedId != null)
            {
                match = _wallets.FirstOrDefault(w => w.Id.Value == previousSelectedId);
            }
            SelectedWallet = match ?? _wallets.FirstOrDefault();

            _logger.LogInformation("WalletContext reloaded: {Count} wallet(s), selected={Selected}",
                _wallets.Count, SelectedWallet?.Name ?? "(none)");

            _walletsUpdated.OnNext(Unit.Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WalletContext.ReloadAsync failed");
        }
        finally
        {
            _isReloading = false;
        }
    }

    public async Task RefreshBalanceAsync(WalletId walletId)
    {
        try
        {
            var result = await _walletAppService.RefreshAndGetAccountBalanceInfo(walletId);
            if (result.IsFailure)
            {
                _logger.LogWarning("RefreshAndGetAccountBalanceInfo failed for {WalletId}: {Error}", walletId.Value, result.Error);
                return;
            }

            var wallet = _wallets.FirstOrDefault(w => w.Id.Value == walletId.Value);
            if (wallet != null)
            {
                wallet.TotalBalanceSats = result.Value.TotalBalance;
                wallet.UnconfirmedBalanceSats = result.Value.TotalUnconfirmedBalance;
                wallet.ReservedBalanceSats = result.Value.TotalBalanceReserved;
            }

            _walletsUpdated.OnNext(Unit.Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RefreshBalanceAsync failed for {WalletId}", walletId.Value);
        }
    }

    public async Task RefreshAllBalancesAsync()
    {
        foreach (var wallet in _wallets.ToList())
        {
            await RefreshBalanceAsync(wallet.Id);
        }
    }

    public async Task DeleteAllAsync()
    {
        try
        {
            var metadatasResult = await _walletAppService.GetMetadatas();
            if (metadatasResult.IsSuccess)
            {
                foreach (var meta in metadatasResult.Value.ToList())
                {
                    _logger.LogInformation("Deleting wallet {WalletId} ('{Name}')", meta.Id.Value, meta.Name);
                    await _walletAppService.DeleteWallet(meta.Id);
                }
            }

            _wallets.Clear();
            SelectedWallet = null;
            _walletsUpdated.OnNext(Unit.Default);
            _logger.LogInformation("WalletContext.DeleteAllAsync completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WalletContext.DeleteAllAsync failed");
        }
    }
}
