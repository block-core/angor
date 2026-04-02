using System.Collections.ObjectModel;
using Angor.Sdk.Common;

namespace App.UI.Shared.Services;

/// <summary>
/// Centralized wallet state singleton. Replaces 7 independent GetMetadatas() calls,
/// 3 duplicate wallet item types, and 20 new WalletId(string) construction sites.
/// </summary>
public interface IWalletContext
{
    /// <summary>All known wallets. Bound directly by UI lists.</summary>
    ReadOnlyObservableCollection<WalletInfo> Wallets { get; }

    /// <summary>Currently selected wallet (persisted across restarts).</summary>
    WalletInfo? SelectedWallet { get; set; }

    /// <summary>Fires after <see cref="Wallets"/> collection changes (reload/delete).</summary>
    IObservable<Unit> WalletsUpdated { get; }

    /// <summary>Cheap reload from LiteDB (GetMetadatas + GetAllAccountBalancesAsync). No network.</summary>
    Task ReloadAsync();

    /// <summary>Refresh a single wallet's balance from the indexer (network call).</summary>
    Task RefreshBalanceAsync(WalletId walletId);

    /// <summary>Refresh all wallet balances from the indexer (network calls).</summary>
    Task RefreshAllBalancesAsync();

    /// <summary>Delete all wallets and clear state. Used by Settings wipe data.</summary>
    Task DeleteAllAsync();
}
