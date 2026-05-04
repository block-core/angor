using System.Security.Cryptography;
using Angor.Data.Documents.Interfaces;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Common;

public class WalletAccountBalanceService(IWalletOperations walletOperations,
    IGenericDocumentCollection<WalletAccountBalanceInfo> collection,
    ISecureKeyProvider secureKeyProvider,
    ILogger<WalletAccountBalanceService> logger) : IWalletAccountBalanceService
{
    public async Task<Result<AccountBalanceInfo>> GetAccountBalanceInfoAsync(WalletId walletId)
    {
        var result = await collection.FindByIdAsync(walletId.Value);
        if (result.IsFailure || result.Value is null)
            return Result.Failure<AccountBalanceInfo>("Account balance not found. Please refresh your wallet.");

        var accountBalanceInfo = result.Value.AccountBalanceInfo;
        await DecryptExtPubKeys(walletId, accountBalanceInfo.AccountInfo);

        return Result.Success(accountBalanceInfo);
    }

    public async Task<Result> SaveAccountBalanceInfoAsync(WalletId walletId, AccountBalanceInfo accountBalanceInfo)
    {
        await EncryptExtPubKeys(walletId, accountBalanceInfo.AccountInfo);

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

        // Clean up pending receive UTXOs that have been confirmed
        CleanupConfirmedPendingReceiveUtxos(accountBalanceInfo);

        // Pass the actual pending receive UTXOs instead of empty list
        accountBalanceInfo.UpdateAccountBalanceInfo(accountBalanceInfo.AccountInfo, accountBalanceInfo.AccountPendingReceive);

        await EncryptExtPubKeys(walletId, accountBalanceInfo.AccountInfo);

        var upsertResult = await collection.UpsertAsync(x => x.WalletId,
            new WalletAccountBalanceInfo { WalletId = walletId.Value, AccountBalanceInfo = accountBalanceInfo });

        if (upsertResult.IsFailure)
            return Result.Failure<AccountBalanceInfo>(upsertResult.Error);

        await DecryptExtPubKeys(walletId, accountBalanceInfo.AccountInfo);

        return Result.Success(accountBalanceInfo);
    }

    /// <summary>
    /// Removes pending receive UTXOs that have been confirmed (now appear in actual UTXO data)
    /// </summary>
    private void CleanupConfirmedPendingReceiveUtxos(AccountBalanceInfo accountBalanceInfo)
    {
        if (accountBalanceInfo.AccountPendingReceive.Count == 0)
            return;

        var currentOutpoints = accountBalanceInfo.AccountInfo.AllUtxos()
                .Select(u => u.outpoint.ToString())
                .ToHashSet();

        // Get all confirmed UTXOs from the account
        var confirmedOutpoints = accountBalanceInfo.AccountInfo.AllUtxos()
                .Where(u => u.blockIndex > 0)
                .Select(u => u.outpoint.ToString())
                .ToHashSet();

        // Remove pending receive UTXOs that are now confirmed or no longer exist in the current UTXO set.
        accountBalanceInfo.AccountPendingReceive.RemoveAll(pending => 
            confirmedOutpoints.Contains(pending.outpoint.ToString()) || !currentOutpoints.Contains(pending.outpoint.ToString()));
    }

    public async Task<Result<IEnumerable<AccountBalanceInfo>>> GetAllAccountBalancesAsync()
    {
        var result = await collection.FindAllAsync();
        if (result.IsFailure)
        {
            logger.LogError("Failed to retrieve all account balances: {Error}", result.Error);
            return Result.Failure<IEnumerable<AccountBalanceInfo>>(result.Error);
        }

        var accountBalances = result.Value.Select(x => x.AccountBalanceInfo).ToList();

        foreach (var accountBalance in accountBalances)
        {
            var walletId = new WalletId(accountBalance.AccountInfo.walletId);
            await DecryptExtPubKeys(walletId, accountBalance.AccountInfo);
        }

        return Result.Success<IEnumerable<AccountBalanceInfo>>(accountBalances);
    }

    public async Task<Result> DeleteAccountBalanceInfoAsync(WalletId walletId)
    {
        var deleteResult = await collection.DeleteAsync(walletId.Value);

        if (!deleteResult.IsFailure)
            return Result.Success();

        logger.LogError("Failed to delete account balance info for wallet {WalletId}: {Error}", walletId, deleteResult.Error);
        return Result.Failure(deleteResult.Error);
    }

    private async Task EncryptExtPubKeys(WalletId walletId, AccountInfo accountInfo)
    {
        var maybeKey = await secureKeyProvider.Get(walletId);
        if (maybeKey.HasNoValue)
        {
            logger.LogWarning("No encryption key found for wallet {WalletId}, storing ExtPubKeys unencrypted", walletId);
            return;
        }

        try
        {
            var key = maybeKey.Value;

            if (!string.IsNullOrEmpty(accountInfo.ExtPubKey))
                accountInfo.ExtPubKey = FieldEncryption.Encrypt(accountInfo.ExtPubKey, key);

            if (!string.IsNullOrEmpty(accountInfo.RootExtPubKey))
                accountInfo.RootExtPubKey = FieldEncryption.Encrypt(accountInfo.RootExtPubKey, key);
        }
        catch (FormatException ex)
        {
            logger.LogWarning(ex, "Invalid encryption key for wallet {WalletId}, storing ExtPubKeys unencrypted", walletId);
        }
    }

    private async Task DecryptExtPubKeys(WalletId walletId, AccountInfo accountInfo)
    {
        var maybeKey = await secureKeyProvider.Get(walletId);
        if (maybeKey.HasNoValue)
        {
            logger.LogWarning("No encryption key found for wallet {WalletId}, assuming ExtPubKeys are unencrypted", walletId);
            return;
        }

        try
        {
            var key = maybeKey.Value;

            if (!string.IsNullOrEmpty(accountInfo.ExtPubKey))
                accountInfo.ExtPubKey = FieldEncryption.Decrypt(accountInfo.ExtPubKey, key);

            if (!string.IsNullOrEmpty(accountInfo.RootExtPubKey))
                accountInfo.RootExtPubKey = FieldEncryption.Decrypt(accountInfo.RootExtPubKey, key);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            logger.LogWarning(ex, "Failed to decrypt ExtPubKeys for wallet {WalletId}, assuming unencrypted", walletId);
        }
    }
}
