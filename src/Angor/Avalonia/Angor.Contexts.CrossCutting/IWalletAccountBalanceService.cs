// Angor.Contexts.CrossCutting/Repositories/IWalletAccountBalanceRepository.cs

using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Contests.CrossCutting;

public interface IWalletAccountBalanceService
{
    Task<Result<AccountBalanceInfo>> GetAccountBalanceAsync(Guid walletId);
    Task<Result> SaveAccountBalanceAsync(Guid walletId, AccountBalanceInfo accountBalanceInfo);
    Task<Result<AccountBalanceInfo>> RefreshAccountBalanceAsync(Guid walletId);
}