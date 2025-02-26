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
        return await GetEncryptedWallet(id)
            .Bind(encryptedWallet =>
                walletSecurityContext.EncryptionKeyProvider.Get(id).ToResult("Encryption key not provided")
                    .Bind(encryptionKey => walletSecurityContext.PassphraseProvider.Get(id).ToResult("Passphrase not provided")
                        .Bind(passphrase => walletSecurityContext.WalletEncryption.Decrypt(encryptedWallet, encryptionKey)
                            .Ensure(data => data.SeedWords != null, "Seed words not found")
                            .Map(data => (data.SeedWords!, passphrase)))));
    }
}