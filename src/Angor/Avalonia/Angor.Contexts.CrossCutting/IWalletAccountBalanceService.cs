using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.CrossCutting;

public interface IWalletAccountBalanceService
{
    Task<Result<AccountBalanceInfo>> GetAccountBalanceInfoAsync(WalletId walletId);
    Task<Result> SaveAccountBalanceInfoAsync(WalletId walletId, AccountBalanceInfo accountBalanceInfo);
    Task<Result<AccountBalanceInfo>> RefreshAccountBalanceInfoAsync(WalletId walletId);
    Task<Result<IEnumerable<AccountBalanceInfo>>> GetAllAccountBalancesAsync();
    Task<Result> DeleteAccountBalanceInfoAsync(WalletId walletId);
}