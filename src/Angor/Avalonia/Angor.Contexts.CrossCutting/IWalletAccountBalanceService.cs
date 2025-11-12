using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Contests.CrossCutting;

public interface IWalletAccountBalanceService
{
    Task<Result<AccountBalanceInfo>> GetAccountBalanceInfoAsync(string walletId);
    Task<Result> SaveAccountBalanceInfoAsync(string walletId, AccountBalanceInfo accountBalanceInfo);
    Task<Result<AccountBalanceInfo>> RefreshAccountBalanceInfoAsync(string walletId);
}