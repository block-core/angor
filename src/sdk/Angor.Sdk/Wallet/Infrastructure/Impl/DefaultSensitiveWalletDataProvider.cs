using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

// A provider wrapper that tries to decrypt with the default encryption password before delegating to the inner provider.
public class FrictionlessSensitiveDataProvider(ISensitiveWalletDataProvider provider, IWalletEncryption walletEncryption, IWalletStore walletStore) : ISensitiveWalletDataProvider
{
    public async Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveData(WalletId walletId)
    {
        var walletsResult = await walletStore.GetAll();
        if (walletsResult.IsFailure)
            return await provider.RequestSensitiveData(walletId);

        var wallet = walletsResult.Value.FirstOrDefault(x => x.Id == walletId.Value);
        if (wallet == null)
            return await provider.RequestSensitiveData(walletId);

        var decryptResult = await walletEncryption.Decrypt(wallet, "DEFAULT");
        if (decryptResult.IsFailure)
            return await provider.RequestSensitiveData(walletId);

        return Result.Success<(string seed, Maybe<string> passphrase)>(
            (decryptResult.Value.SeedWords!, Maybe<string>.None));
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