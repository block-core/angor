using System.Security.Cryptography;
using Angor.Contexts.CrossCutting;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Interfaces;

public class LocalPasswordProvider(IEncryptionKeyStore keyStore) : IPasswordProvider
{
    public async Task<Maybe<string>> Get(WalletId walletId)
    {
        var key = await keyStore.GetKeyAsync(walletId);

        if (key is null)
        {
            // Generate a new random encryption key (256-bit) and persist it
            var newKey = GenerateRandomKey();
            await keyStore.SaveKeyAsync(walletId, newKey);
            key = newKey;
        }

        return Maybe<string>.From(key);
    }

    private static string GenerateRandomKey()
    {
        const int keySizeBytes = 32; // 256 bits
        var bytes = RandomNumberGenerator.GetBytes(keySizeBytes);
        return Convert.ToBase64String(bytes);
    }
}