using System.Security.Cryptography;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class FileSecureKeyProvider(IStore store) : ISecureKeyProvider
{
    private const string KeysFile = "wallet-keys.json";

    public async Task<Maybe<string>> Get(WalletId walletId)
    {
        var keysResult = await LoadKeys();
        if (keysResult.IsFailure)
            return Maybe<string>.None;

        return keysResult.Value.TryGetValue(walletId.Value, out var key)
            ? Maybe<string>.From(key)
            : Maybe<string>.None;
    }

    public async Task Save(WalletId walletId, string key)
    {
        var keys = (await LoadKeys()).GetValueOrDefault(new Dictionary<string, string>());
        keys[walletId.Value] = key;
        await store.Save(KeysFile, keys);
    }

    public async Task Remove(WalletId walletId)
    {
        var keysResult = await LoadKeys();
        if (keysResult.IsFailure)
            return;

        var keys = keysResult.Value;
        if (keys.Remove(walletId.Value))
        {
            await store.Save(KeysFile, keys);
        }
    }

    public string GenerateKey()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private Task<Result<Dictionary<string, string>>> LoadKeys()
    {
        return store.Load<Dictionary<string, string>>(KeysFile)
            .OnFailureCompensate(_ => new Dictionary<string, string>());
    }
}
