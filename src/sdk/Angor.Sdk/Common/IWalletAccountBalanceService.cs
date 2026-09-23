using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Common;

public interface IWalletAccountBalanceService
{
    Task<Result<AccountBalanceInfo>> GetAccountBalanceInfoAsync(WalletId walletId);
    Task<Result> SaveAccountBalanceInfoAsync(WalletId walletId, AccountBalanceInfo accountBalanceInfo);
    Task<Result<AccountBalanceInfo>> RefreshAccountBalanceInfoAsync(WalletId walletId);

    /// <summary>
    /// Lightweight variant used when we only need the next unused receive address (e.g. to display
    /// in the Receive modal). Unlike <see cref="RefreshAccountBalanceInfoAsync"/>, this does NOT re-fetch
    /// UTXO data for every address the wallet has ever used - it only runs the bounded gap-limit scan
    /// (starting at the last known unused address index) required to confirm/advance the next receive
    /// address. This keeps the call fast even for wallets with a large transaction history.
    /// </summary>
    Task<Result<AccountBalanceInfo>> RefreshNextReceiveAddressAsync(WalletId walletId);

    Task<Result<IEnumerable<AccountBalanceInfo>>> GetAllAccountBalancesAsync();
    Task<Result> DeleteAccountBalanceInfoAsync(WalletId walletId);
}