using Angor.Contexts.CrossCutting;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class DefaultSensitiveWalletDataProvider(ISensitiveWalletDataProvider provider, IWalletEncryption walletEncryption, IWalletStore walletStore) : ISensitiveWalletDataProvider
{
    public Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveData(WalletId walletId)
    {
        return walletStore.GetAll()
            .Bind(wallets => wallets.TryFirst().ToResult("Wallet not found")) // Get the first wallet
            .Bind(wallet => walletEncryption.Decrypt(wallet, "DEFAULT")) // Try to decrypt the wallet with the default encryption key ("DEFAULT")
            .Map(data => (data.SeedWords, Maybe<string>.None))  // On success, return its seedwords (assume no passphrase)
            .Compensate(_ => provider.RequestSensitiveData(walletId));  // On failure, delegate to the flow to the inner provider. This is, the default encryption key failed to decrypt the wallet didn't work.
    }

    public void SetSensitiveData(WalletId id, (string seed, Maybe<string> passphrase) data)
    {
        provider.SetSensitiveData(id, data);
    }

    public void RemoveSensitiveData(WalletId id)
    {
        provider.RemoveSensitiveData(id);
    }
}