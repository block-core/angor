using Angor.Contexts.CrossCutting;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Contests.CrossCutting;

public class WalletAccountBalanceService(IWalletOperations walletOperations,
    IGenericDocumentCollection<WalletAccountBalanceInfo> collection,
    ILogger<WalletAccountBalanceService> logger) : IWalletAccountBalanceService
{
    public async Task<Result<AccountBalanceInfo>> GetAccountBalanceInfoAsync(string walletId)
    {
        var result = await collection.FindByIdAsync(walletId);
        if (result.IsFailure || result.Value is null)
            return Result.Failure<AccountBalanceInfo>("Account balance not found. Please refresh your wallet.");
        
        return Result.Success(result.Value.AccountBalanceInfo);
    }

    public async Task<Result> SaveAccountBalanceInfoAsync(string walletId, AccountBalanceInfo accountBalanceInfo)
    {
        var upsertResult = await collection.UpsertAsync(x => x.WalletId,
            new WalletAccountBalanceInfo { WalletId = walletId, AccountBalanceInfo = accountBalanceInfo });

        if (!upsertResult.IsFailure) 
            return Result.Success(accountBalanceInfo);
        
        logger.LogError("Failed to save account balance info for wallet {WalletId}: {Error}", walletId, upsertResult.Error);
        return Result.Failure<AccountBalanceInfo>(upsertResult.Error);
    }

    public async Task<Result<AccountBalanceInfo>> RefreshAccountBalanceInfoAsync(string walletId)
    {
        var accountBalanceInfoResult = await GetAccountBalanceInfoAsync(walletId);
        if (accountBalanceInfoResult.IsFailure)
            return accountBalanceInfoResult;
        
        var accountBalanceInfo = accountBalanceInfoResult.Value;
        
        var updateResult = await Result.Try(async () =>
        {
            await walletOperations.UpdateDataForExistingAddressesAsync(accountBalanceInfo.AccountInfo);
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountBalanceInfo.AccountInfo);
        });

        if (updateResult.IsFailure)
            return Result.Failure<AccountBalanceInfo>(updateResult.Error);
        
        accountBalanceInfo.UpdateAccountBalanceInfo(accountBalanceInfo.AccountInfo, []);
        
        var upsertResult = await collection.UpsertAsync(x => x.WalletId,
            new WalletAccountBalanceInfo { WalletId = walletId, AccountBalanceInfo = accountBalanceInfo });

        return !upsertResult.IsFailure ? Result.Success(accountBalanceInfo) : Result.Failure<AccountBalanceInfo>(upsertResult.Error);
    }
}