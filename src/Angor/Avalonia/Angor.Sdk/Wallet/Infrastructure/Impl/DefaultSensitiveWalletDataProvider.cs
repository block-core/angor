using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

// A provider wrapper that tries to decrypt with the default encryption password before delegating to the inner provider.
public class FrictionlessSensitiveDataProvider(ISensitiveWalletDataProvider provider, IWalletEncryption walletEncryption, IWalletStore walletStore) : ISensitiveWalletDataProvider
{
    public Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveData(WalletId walletId)
    {
        return walletStore.GetAll()
                          .Bind(wallets => wallets.TryFirst().ToResult("Wallet not found"))
                          .Bind(wallet => walletEncryption.DecryptAsync(wallet, "DEFAULT"))
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