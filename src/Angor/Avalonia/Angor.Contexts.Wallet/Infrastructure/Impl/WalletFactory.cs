using System.Text.Json;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class WalletFactory(IWalletStore walletStore, IWalletSecurityContext securityContext)
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

        var encryptedWallet = await securityContext.WalletEncryption
            .Encrypt(walletData, encryptionKey, name, walletId.Id);

        return await walletStore.GetAll()
            .Map(existing => existing.Append(encryptedWallet))
            .Bind(wallets => walletStore.SaveAll(wallets))
            .Map(() => wallet);
    }
}