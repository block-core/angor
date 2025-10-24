using System.Text.Json;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class WalletFactory(
    IWalletStore walletStore, 
    ISensitiveWalletDataProvider sensitiveWalletDataProvider, 
    IWalletSecurityContext securityContext,
    IWalletOperations walletOperations,
    IWalletAccountBalanceService accountBalanceService,
    IDerivationOperations derivationOperations, 
    INetworkConfiguration networkConfiguration,
    IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeysCollection)
    : IWalletFactory
{
    public async Task<Result<Domain.Wallet>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, string encryptionKey, BitcoinNetwork network)
    {
        var walletId = WalletAppService.SingleWalletId;
        var descriptor = WalletDescriptorFactory.Create(seedwords, passphrase, network.ToNBitcoin());
        var wallet = new Domain.Wallet(walletId, descriptor);
        
        sensitiveWalletDataProvider.SetSensitiveData(walletId, (seedwords, passphrase));

        var walletData = new WalletData
        {
            DescriptorJson = JsonSerializer.Serialize(descriptor.ToDto()),
            RequiresPassphrase = passphrase.HasValue,
            SeedWords = seedwords//
        };
        
        var saveResult = await SaveEncryptedWalletToStoreAsync(name, encryptionKey, walletData, walletId);

        if (saveResult.IsFailure)
            return Result.Failure<Domain.Wallet>(saveResult.Error);

        var walletWords = new WalletWords { Words = seedwords, Passphrase = passphrase.GetValueOrDefault() };
        
        var accountInfoResult = await CreateAccountInfoAsync(walletWords, passphrase, walletId);
        
        if (accountInfoResult.IsFailure)
            return Result.Failure<Domain.Wallet>(accountInfoResult.Error);

        var keysCreated = await CreateFounderKeyCollectionAsync(walletWords, walletId);

        return !keysCreated.IsSuccess 
            ? Result.Failure<Domain.Wallet>(keysCreated.Error) 
            : Result.Success(wallet);
    }

    private async Task<Result> SaveEncryptedWalletToStoreAsync(string name, string encryptionKey, WalletData walletData,
        WalletId walletId)
    {
        var encryptedWallet = await securityContext.WalletEncryption
            .Encrypt(walletData, encryptionKey, name, walletId.Value);

        return await walletStore.GetAll()
            .Map(existing => existing.Append(encryptedWallet))
            .Bind(wallets => walletStore.SaveAll(wallets));
    }

    private async Task<Result> CreateAccountInfoAsync(WalletWords walletWords, Maybe<string> passphrase, WalletId walletId)
    {
        // Create initial account balance info
        
        var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
        
        var accountBalanceInfo = new AccountBalanceInfo();
        accountBalanceInfo.UpdateAccountBalanceInfo(accountInfo, []);

        var saveToDbResult = await accountBalanceService.SaveAccountBalanceInfoAsync(walletId.Value, accountBalanceInfo);
        if (saveToDbResult.IsFailure)
            return Result.Failure(saveToDbResult.Error);
        
        return await accountBalanceService.RefreshAccountBalanceInfoAsync(walletId.Value);
    }

    private async Task<Result> CreateFounderKeyCollectionAsync(WalletWords walletWords, WalletId walletId)
    {
        var founderKeys = derivationOperations.DeriveProjectKeys(walletWords, networkConfiguration.GetAngorKey());

        var derivedKeys = new DerivedProjectKeys
        {
            WalletId = walletId.Value.ToString(),
            Keys = founderKeys.Keys
        };
            
        return await derivedProjectKeysCollection.UpsertAsync(x => x.WalletId, derivedKeys);
    }
}
