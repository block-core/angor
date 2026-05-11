using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Primitives;


namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class SensitiveWalletDataProvider(IWalletStore walletStore, IWalletSecurityContext walletSecurityContext,
    IWalletEncryption walletEncryption) : ISensitiveWalletDataProvider
{
    private readonly Dictionary<WalletId, (string, string?)> cachedSensitiveData = new();

    public async Task<Result<(string seed, string? passphrase)>> RequestSensitiveData(WalletId walletId)
    {
        if (cachedSensitiveData.TryGetValue(walletId, out var cached))
        {
            return cached;
        }

        var result = await RequestSensitiveDataCore(walletId);
        if (result.IsSuccess)
        {
            cachedSensitiveData[walletId] = result.Value;
        }

        return result;
    }

    public void SetSensitiveData(WalletId id, (string seed, string? passphrase) data)
    {
        cachedSensitiveData[id] = data;
    }

    public void RemoveSensitiveData(WalletId id)
    {
        cachedSensitiveData.Remove(id);
    }

    private async Task<Result<EncryptedWallet>> GetEncryptedWallet(WalletId id)
    {
        var allResult = await walletStore.GetAll();
        if (allResult.IsFailure)
            return Result.Failure<EncryptedWallet>(allResult.Error);

        var wallet = allResult.Value.FirstOrDefault(x => x.Id == id.Value);
        if (wallet == null)
            return Result.Failure<EncryptedWallet>("Wallet not found");

        return Result.Success(wallet);
    }

    private async Task<Result<(string seed, string? passphrase)>> RequestSensitiveDataCore(WalletId id)
    {
        // Get the encrypted wallet
        var encryptedWalletResult = await GetEncryptedWallet(id);
        if (encryptedWalletResult.IsFailure)
            return Result.Failure<(string, string?)>(encryptedWalletResult.Error);

        // Get the encryption key
        var encryptionKey = await walletSecurityContext.PasswordProvider.Get(id);
        if (encryptionKey == null)
            return Result.Failure<(string, string?)>("Encryption key not provided");


        // Decrypt the wallet
        var decryptedResult = await walletEncryption.Decrypt(encryptedWalletResult.Value, encryptionKey);
        if (decryptedResult.IsFailure)
        {
            return Result.Failure<(string, string?)>("Invalid encryption key");
        }

        string? passphrase = null;
        if (decryptedResult.Value.RequiresPassphrase)
        {
            var providedPassphrase = await walletSecurityContext.PassphraseProvider.Get(id);
            if (providedPassphrase == null)
            {
                return Result.Failure<(string, string?)>("Passphrase not provided");
            }

            passphrase = providedPassphrase;
        }

        (string seedWords, string?) valueTuple = (decryptedResult.Value.SeedWords!, passphrase);

        return Result.Success(valueTuple);
    }
}
