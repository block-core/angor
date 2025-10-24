// Angor.Contexts.CrossCutting/Repositories/IWalletAccountBalanceRepository.cs

using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Contests.CrossCutting;

public interface IWalletAccountBalanceService
{
    Task<Result<AccountBalanceInfo>> GetAccountBalanceInfoAsync(Guid walletId);
    Task<Result> SaveAccountBalanceInfoAsync(Guid walletId, AccountBalanceInfo accountBalanceInfo);
    Task<Result<AccountBalanceInfo>> RefreshAccountBalanceInfoAsync(Guid walletId);
}