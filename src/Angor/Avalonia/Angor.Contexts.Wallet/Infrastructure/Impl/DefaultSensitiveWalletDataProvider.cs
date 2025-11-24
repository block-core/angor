using Angor.Contexts.CrossCutting;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class DefaultSensitiveWalletDataProvider(ISensitiveWalletDataProvider provider, IWalletEncryption walletEncryption, IWalletStore walletStore) : ISensitiveWalletDataProvider
{
    public Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveData(WalletId walletId)
    {
        return walletStore.GetAll()
            .Bind(wallets => wallets.TryFirst().ToResult("Wallet not found"))
            .Bind(wallet => walletEncryption.Decrypt(wallet, "DEFAULT"))
            .Map(data => (data.SeedWords, Maybe<string>.None))
            .Compensate(_ => provider.RequestSensitiveData(walletId));
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