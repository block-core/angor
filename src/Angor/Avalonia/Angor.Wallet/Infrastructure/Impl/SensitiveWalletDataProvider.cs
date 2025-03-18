using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.ValueTasks;

namespace Angor.Wallet.Infrastructure.Impl;

public class SensitiveWalletDataProvider(IWalletStore walletStore, IWalletSecurityContext walletSecurityContext) : ISensitiveWalletDataProvider
{
    private readonly Dictionary<WalletId, (string, Maybe<string>)> sensitiveData = new();

    public async Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveData(WalletId id)
    {
        var findResult = sensitiveData.TryFind(id);
        if (findResult.HasValue)
        {
            return findResult.Value;
        }

        var result = await RequestSensitiveDataCore(id).Tap(data => sensitiveData[id] = data);
        return result;
    }

    private async Task<Result<EncryptedWallet>> GetEncryptedWallet(WalletId id)
    {
        return await walletStore.GetAll()
            .Map(list => list.FirstOrDefault(x => x.Id == id.Id))
            .EnsureNotNull("Wallet not found");
    }

    private async Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveDataCore(WalletId id)
    {
        // Get the encrypted wallet
        var encryptedWalletResult = await GetEncryptedWallet(id);
        if (encryptedWalletResult.IsFailure)
            return Result.Failure<(string, Maybe<string>)>(encryptedWalletResult.Error);

        // Get the encryption key
        var encryptionKey = await walletSecurityContext.EncryptionKeyProvider.Get(id);
        if (encryptionKey.HasNoValue)
            return Result.Failure<(string, Maybe<string>)>("Encryption key not provided");
        

        // Decrypt the wallet
        var decryptedResult = await walletSecurityContext.WalletEncryption.Decrypt(encryptedWalletResult.Value, encryptionKey.Value);
        if (decryptedResult.IsFailure || decryptedResult.Value.SeedWords == null)
            return Result.Failure<(string, Maybe<string>)>("Seed words not found");

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