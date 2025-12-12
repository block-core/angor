using Angor.Sdk.Common;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Common;

public class WalletAccountBalanceService(IWalletOperations walletOperations,
    IGenericDocumentCollection<WalletAccountBalanceInfo> collection,
    ILogger<WalletAccountBalanceService> logger) : IWalletAccountBalanceService
{
    public async Task<Result<AccountBalanceInfo>> GetAccountBalanceInfoAsync(WalletId walletId)
    {
        var result = await collection.FindByIdAsync(walletId.Value);
        if (result.IsFailure || result.Value is null)
            return Result.Failure<AccountBalanceInfo>("Account balance not found. Please refresh your wallet.");
        
        return Result.Success(result.Value.AccountBalanceInfo);
    }

    public async Task<Result> SaveAccountBalanceInfoAsync(WalletId walletId, AccountBalanceInfo accountBalanceInfo)
    {
        var upsertResult = await collection.UpsertAsync(x => x.WalletId,
            new WalletAccountBalanceInfo { WalletId = walletId.Value, AccountBalanceInfo = accountBalanceInfo });

        if (!upsertResult.IsFailure) 
            return Result.Success(accountBalanceInfo);
        
        logger.LogError("Failed to save account balance info for wallet {WalletId}: {Error}", walletId, upsertResult.Error);
        return Result.Failure<AccountBalanceInfo>(upsertResult.Error);
    }

    public async Task<Result<AccountBalanceInfo>> RefreshAccountBalanceInfoAsync(WalletId walletId)
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
            new WalletAccountBalanceInfo { WalletId = walletId.Value, AccountBalanceInfo = accountBalanceInfo });

        return !upsertResult.IsFailure ? Result.Success(accountBalanceInfo) : Result.Failure<AccountBalanceInfo>(upsertResult.Error);
    }

    public async Task<Result<IEnumerable<AccountBalanceInfo>>> GetAllAccountBalancesAsync()
    {
        var result = await collection.FindAllAsync();
        if (result.IsFailure)
        {
            logger.LogError("Failed to retrieve all account balances: {Error}", result.Error);
            return Result.Failure<IEnumerable<AccountBalanceInfo>>(result.Error);
        }

        var accountBalances = result.Value.Select(x => x.AccountBalanceInfo);
        return Result.Success(accountBalances);
    }

    public async Task<Result> DeleteAccountBalanceInfoAsync(WalletId walletId)
    {
        var deleteResult = await collection.DeleteAsync(walletId.Value);

        if (!deleteResult.IsFailure)
            return Result.Success();

        logger.LogError("Failed to delete account balance info for wallet {WalletId}: {Error}", walletId, deleteResult.Error);
        return Result.Failure(deleteResult.Error);
    }
}