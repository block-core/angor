using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.ValueTasks;

namespace Angor.Wallet.Infrastructure.Impl;

public class SensitiveWalletDataProvider(IWalletStore walletStore, IWalletSecurityContext walletSecurityContext) : ISensitiveWalletDataProvider
{
    private readonly Dictionary<WalletId, (string, string)> sensitiveData = new();

    public async Task<Result<(string seed, string passphrase)>> RequestSensitiveData(WalletId id)
    {
        var findResult = sensitiveData.TryFind(id);
        if (findResult.HasValue)
        {
            return findResult.Value;
        }

        return await RequestSensitiveDataCore(id).Tap(data => sensitiveData[id] = data);
    }

    private async Task<Result<EncryptedWallet>> GetEncryptedWallet(WalletId id)
    {
        return await walletStore.GetAll()
            .Map(list => list.FirstOrDefault(x => x.Id == id.Id))
            .EnsureNotNull("Wallet not found");
    }

    private async Task<Result<(string seed, string passphrase)>> RequestSensitiveDataCore(WalletId id)
    {
        // Get the encrypted wallet
        var encryptedWalletResult = await GetEncryptedWallet(id);
        if (encryptedWalletResult.IsFailure)
            return Result.Failure<(string, string)>(encryptedWalletResult.Error);

        // Get the encryption key
        var encryptionKey = await walletSecurityContext.EncryptionKeyProvider.Get(id);
        if (encryptionKey.HasNoValue)
            return Result.Failure<(string, string)>("Encryption key not provided");

        // Get the passphrase
        var passphrase = await walletSecurityContext.PassphraseProvider.Get(id);
        if (passphrase.HasNoValue)
            return Result.Failure<(string, string)>("Passphrase not provided");

        // Decrypt the wallet
        var decryptedResult = await walletSecurityContext.WalletEncryption.Decrypt(encryptedWalletResult.Value, encryptionKey.Value);
        if (decryptedResult.IsFailure || decryptedResult.Value.SeedWords == null)
            return Result.Failure<(string, string)>("Seed words not found");

        return Result.Success((decryptedResult.Value.SeedWords, passphrase.Value));
    }

}