using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Primitives;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class WalletFactory(
    IWalletStore walletStore,
    ISensitiveWalletDataProvider sensitiveWalletDataProvider,
    IWalletOperations walletOperations,
    IWalletAccountBalanceService accountBalanceService,
    IDerivationOperations derivationOperations,
    INetworkConfiguration networkConfiguration,
    IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeysCollection,
    IWalletEncryption walletEncryption,
    ISecureKeyProvider secureKeyProvider)
    : IWalletFactory
{
    public async Task<Result<Domain.Wallet>> CreateWallet(string name, string seedwords, string? passphrase, BitcoinNetwork network)
    {
        // Derive the wallet ID from the master public key (xpub) hash
        var walletWords = new WalletWords { Words = seedwords, Passphrase = passphrase ?? "" };
        var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
        var walletId = new WalletId(accountInfo.walletId);

        // Generate and persist a secure random key used for all wallet encryption
        var encryptionKey = secureKeyProvider.GenerateKey();
        await secureKeyProvider.Save(walletId, encryptionKey);
        var descriptor = WalletDescriptorFactory.Create(seedwords, passphrase, network.ToNBitcoin());
        var wallet = new Domain.Wallet(walletId, descriptor);

        sensitiveWalletDataProvider.SetSensitiveData(walletId, (seedwords, passphrase));

        // If account balance info already exists in the database, the wallet is already active
        var existingBalance = await accountBalanceService.GetAccountBalanceInfoAsync(walletId);
        if (existingBalance.IsSuccess)
            return Result.Success(wallet);

        var walletData = new WalletData
        {
            DescriptorJson = JsonSerializer.Serialize(descriptor.ToDto()),
            RequiresPassphrase = passphrase != null,
            SeedWords = seedwords
        };

        var saveResult = await SaveEncryptedWalletToStoreAsync(name, encryptionKey, walletData, walletId);

        if (saveResult.IsFailure)
            return Result.Failure<Domain.Wallet>(saveResult.Error);
        
        var accountInfoResult = await CreateAccountBalanceInfoAsync(accountInfo, walletId);
        
        if (accountInfoResult.IsFailure)
            return Result.Failure<Domain.Wallet>(accountInfoResult.Error);

        var keysCreated = await RebuildFounderKeysAsync(walletWords, walletId);

        return !keysCreated.IsSuccess 
            ? Result.Failure<Domain.Wallet>(keysCreated.Error) 
            : Result.Success(wallet);
    }
    
    private async Task<Result> SaveEncryptedWalletToStoreAsync(string name, string encryptionKey, WalletData walletData,
        WalletId walletId)
    {
        var existing = await walletStore.GetAll();
        if (existing.IsSuccess && existing.Value.Any(w => w.Id == walletId.Value))
            return Result.Success();

        var encryptedWallet = await walletEncryption
            .Encrypt(walletData, encryptionKey, name, walletId.Value);

        var getAllResult = await walletStore.GetAll();
        if (getAllResult.IsFailure)
            return Result.Failure(getAllResult.Error);

        var updatedWallets = getAllResult.Value.Append(encryptedWallet);
        return await walletStore.SaveAll(updatedWallets);
    }

    private Task<Result> CreateAccountBalanceInfoAsync(AccountInfo accountInfo,WalletId walletId)
    {
        // Create initial account balance info
        var accountBalanceInfo = new AccountBalanceInfo();
        accountBalanceInfo.UpdateAccountBalanceInfo(accountInfo, []);

        return accountBalanceService.SaveAccountBalanceInfoAsync(walletId, accountBalanceInfo);
    }

    public async Task<Result> RebuildFounderKeysAsync(WalletWords walletWords, WalletId walletId)
    {
        var founderKeys = derivationOperations.DeriveProjectKeys(walletWords, networkConfiguration.GetAngorKey());

        var derivedKeys = new DerivedProjectKeys
        {
            WalletId = walletId.Value,
            Keys = founderKeys.Keys
        };
            
        var upsertResult = await derivedProjectKeysCollection.UpsertAsync(x => x.WalletId, derivedKeys);
        if (upsertResult.IsFailure)
            return Result.Failure(upsertResult.Error);
        return Result.Success();
    }
}
