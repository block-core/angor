using System.Text.Json;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class WalletFactory(
    IWalletStore walletStore, 
    ISensitiveWalletDataProvider sensitiveWalletDataProvider, 
    IWalletSecurityContext securityContext,
    IWalletOperations walletOperations,
    IWalletAccountBalanceService accountBalanceService)
    : IWalletFactory
{
    public async Task<Result<Domain.Wallet>> CreateWallet(string name, string seedwords, Maybe<string> passphrase, string encryptionKey, BitcoinNetwork network)
    {
        var walletId = WalletAppService.SingleWalletId;
        var descriptor = WalletDescriptorFactory.Create(seedwords, passphrase, network.ToNBitcoin());
        var wallet = new Domain.Wallet(walletId, descriptor);
        
        var walletData = new WalletData
        {
            DescriptorJson = JsonSerializer.Serialize(descriptor.ToDto()),
            RequiresPassphrase = passphrase.HasValue,
            SeedWords = seedwords
        };
        
        sensitiveWalletDataProvider.SetSensitiveData(walletId, (seedwords, passphrase));

        var encryptedWallet = await securityContext.WalletEncryption
            .Encrypt(walletData, encryptionKey, name, walletId.Value);

        var saveResult = await walletStore.GetAll()
            .Map(existing => existing.Append(encryptedWallet))
            .Bind(wallets => walletStore.SaveAll(wallets));

        if (saveResult.IsFailure)
            return Result.Failure<Domain.Wallet>(saveResult.Error);

        // Create initial account balance info
        var walletWords = new WalletWords { Words = seedwords, Passphrase = passphrase.GetValueOrDefault() };
        var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
        
        var accountBalanceInfo = new AccountBalanceInfo();
        accountBalanceInfo.UpdateAccountBalanceInfo(accountInfo, []);

        var resultingBalance = await accountBalanceService.RefreshAccountBalanceInfoAsync(walletId.Value);

        if (!resultingBalance.IsSuccess)
            return Result.Failure<Domain.Wallet>(resultingBalance.Error);

        var savedResult = await accountBalanceService.SaveAccountBalanceInfoAsync(walletId.Value, accountBalanceInfo);

        return !savedResult.IsSuccess ? Result.Failure<Domain.Wallet>(savedResult.Error) : Result.Success(wallet);
    }
}
