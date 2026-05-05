using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class WalletFactory(
    IWalletStore walletStore, 
    ISensitiveWalletDataProvider sensitiveWalletDataProvider, 
    IWalletOperations walletOperations,
    IWalletAccountBalanceService accountBalanceService,
    IDerivationOperations derivationOperations, 
    INetworkConfiguration networkConfiguration,
    IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeysCollection,
    IWalletEncryption walletEncryption)
    : IWalletFactory
{
    public async Task<Result<Domain.Wallet>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, string encryptionKey, BitcoinNetwork network)
    {
        // Derive the wallet ID from the master public key (xpub) hash
        var walletWords = new WalletWords { Words = seedwords, Passphrase = passphrase.GetValueOrDefault() };
        var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
        var walletId = new WalletId(accountInfo.walletId);

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
            RequiresPassphrase = passphrase.HasValue,
            SeedWords = seedwords//
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

        return await walletStore.GetAll()
            .Map(wallets => wallets.Append(encryptedWallet))
            .Bind(wallets => walletStore.SaveAll(wallets));
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
            
        return await derivedProjectKeysCollection.UpsertAsync(x => x.WalletId, derivedKeys);
    }
}
