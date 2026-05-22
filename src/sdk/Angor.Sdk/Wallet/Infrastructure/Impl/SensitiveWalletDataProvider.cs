using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.ValueTasks;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

/// <summary>
/// Caches wallet sensitive data (mnemonic words, passphrase) using WalletWords
/// which backs storage with char[] arrays that can be zeroed on disposal.
/// </summary>
public class SensitiveWalletDataProvider(IWalletStore walletStore, IWalletSecurityContext walletSecurityContext,
    IWalletEncryption walletEncryption) : ISensitiveWalletDataProvider, IDisposable
{
    private readonly Dictionary<WalletId, WalletWords> cachedWalletWords = new();

    public async Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveData(WalletId walletId)
    {
        if (cachedWalletWords.TryGetValue(walletId, out var cached))
        {
            return ToTuple(cached);
        }

        var result = await RequestSensitiveDataCore(walletId);
        if (result.IsSuccess)
        {
            var walletWords = new WalletWords
            {
                Words = result.Value.seed,
                Passphrase = result.Value.passphrase.GetValueOrDefault("")
            };
            cachedWalletWords[walletId] = walletWords;
        }

        return result;
    }

    public void SetSensitiveData(WalletId id, (string seed, Maybe<string> passphrase) data)
    {
        // Dispose any previously cached data for this wallet
        if (cachedWalletWords.TryGetValue(id, out var existing))
            existing.Dispose();

        cachedWalletWords[id] = new WalletWords
        {
            Words = data.seed,
            Passphrase = data.passphrase.GetValueOrDefault("")
        };
    }

    public void RemoveSensitiveData(WalletId id)
    {
        if (cachedWalletWords.Remove(id, out var walletWords))
            walletWords.Dispose();
    }

    public void Dispose()
    {
        foreach (var walletWords in cachedWalletWords.Values)
            walletWords.Dispose();

        cachedWalletWords.Clear();
    }

    private static (string seed, Maybe<string> passphrase) ToTuple(WalletWords walletWords)
    {
        Maybe<string> passphrase = string.IsNullOrEmpty(walletWords.Passphrase)
            ? Maybe<string>.None
            : walletWords.Passphrase;
        return (walletWords.Words, passphrase);
    }

    private async Task<Result<EncryptedWallet>> GetEncryptedWallet(WalletId id)
    {
        return await walletStore.GetAll()
            .Map(list => list.FirstOrDefault(x => x.Id == id.Value))
            .EnsureNotNull("Wallet not found");
    }

    private async Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveDataCore(WalletId id)
    {
        // Get the encrypted wallet
        var encryptedWalletResult = await GetEncryptedWallet(id);
        if (encryptedWalletResult.IsFailure)
            return Result.Failure<(string, Maybe<string>)>(encryptedWalletResult.Error);

        // Get the encryption key
        var encryptionKey = await walletSecurityContext.PasswordProvider.Get(id);
        if (encryptionKey.HasNoValue)
            return Result.Failure<(string, Maybe<string>)>("Encryption key not provided");
        

        // Decrypt the wallet
        var decryptedResult = await walletEncryption.Decrypt(encryptedWalletResult.Value, encryptionKey.Value);
        if (decryptedResult.IsFailure)
        {
            return Result.Failure<(string, Maybe<string>)>("Invalid encryption key");
        }

        Maybe<string> passphrase = Maybe<string>.None;
        if (decryptedResult.Value.RequiresPassphrase)
        {
            var providedPassphrase = await walletSecurityContext.PassphraseProvider.Get(id);
            if (providedPassphrase.HasNoValue)
            {
                return Result.Failure<(string, Maybe<string>)>("Passphrase not provided");
            }

            passphrase = providedPassphrase.Value;
        }

        (string seedWords, Maybe<string>) valueTuple = (decryptedResult.Value.SeedWords!, passphrase);
        
        return Result.Success(valueTuple);
    }
}
