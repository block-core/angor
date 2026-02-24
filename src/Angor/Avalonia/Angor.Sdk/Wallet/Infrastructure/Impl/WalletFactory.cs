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
  public async Task<Result<Domain.Wallet>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, BitcoinNetwork network)
    {
        var walletWords = new WalletWords { Words = seedwords, Passphrase = passphrase.GetValueOrDefault() };
        var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
        var walletId = new WalletId(accountInfo.walletId);

        var descriptor = WalletDescriptorFactory.Create(seedwords, passphrase, network.ToNBitcoin());
        var wallet = new Domain.Wallet(walletId, descriptor);

        sensitiveWalletDataProvider.SetSensitiveData(walletId, (seedwords, passphrase));

        var walletData = new WalletData
        {
            DescriptorJson = JsonSerializer.Serialize(descriptor.ToDto()),
            RequiresPassphrase = passphrase.HasValue,
            SeedWords = seedwords
        };

        var encryptResult = await walletEncryption.EncryptWithStoredKeyAsync(walletData, walletId.Value, name);
        if (encryptResult.IsFailure)
            return Result.Failure<Domain.Wallet>(encryptResult.Error);

        // remove existing entry with same ID before saving — safe on retry
        var allResult = await walletStore.GetAll();
        if (allResult.IsFailure)
            return Result.Failure<Domain.Wallet>(allResult.Error);

        var updatedWallets = allResult.Value
            .Where(w => w.Id != encryptResult.Value.Id)
            .Append(encryptResult.Value);

        var saveResult = await walletStore.SaveAll(updatedWallets);
        if (saveResult.IsFailure)
            return Result.Failure<Domain.Wallet>(saveResult.Error);

        var accountInfoResult = await CreateAccountBalanceInfoAsync(accountInfo, walletId);
        if (accountInfoResult.IsFailure)
            return Result.Failure<Domain.Wallet>(accountInfoResult.Error);

        var keysCreated = await CreateFounderKeyCollectionAsync(walletWords, walletId);

        return keysCreated.IsSuccess
            ? Result.Success(wallet)
            : Result.Failure<Domain.Wallet>(keysCreated.Error);
    }
    private async Task<Result> SaveEncryptedWalletToStoreAsync(string name, string encryptionKey, WalletData walletData, WalletId walletId)
    {
        var encryptResult = await walletEncryption.EncryptAsync(walletData, encryptionKey, name, walletId.Value);

        if (encryptResult.IsFailure)
            return Result.Failure(encryptResult.Error);

        return await walletStore.GetAll()
            .Map(existing => existing.Append(encryptResult.Value))
            .Bind(wallets => walletStore.SaveAll(wallets));
    }

    private Task<Result> CreateAccountBalanceInfoAsync(AccountInfo accountInfo, WalletId walletId)
    {
        var accountBalanceInfo = new AccountBalanceInfo();
        accountBalanceInfo.UpdateAccountBalanceInfo(accountInfo, []);
        return accountBalanceService.SaveAccountBalanceInfoAsync(walletId, accountBalanceInfo);
    }

    private async Task<Result> CreateFounderKeyCollectionAsync(WalletWords walletWords, WalletId walletId)
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