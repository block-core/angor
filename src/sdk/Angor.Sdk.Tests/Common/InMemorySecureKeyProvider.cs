using System.Security.Cryptography;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Primitives;

namespace Angor.Sdk.Tests.Common;

public class InMemorySecureKeyProvider : ISecureKeyProvider
{
    private readonly Dictionary<string, string> _keys = new();

    public Task<string?> Get(WalletId walletId)
    {
        return Task.FromResult(_keys.TryGetValue(walletId.Value, out var key)
            ? key
            : (string?)null);
    }

    public Task Save(WalletId walletId, string key)
    {
        _keys[walletId.Value] = key;
        return Task.CompletedTask;
    }

    public Task Remove(WalletId walletId)
    {
        _keys.Remove(walletId.Value);
        return Task.CompletedTask;
    }

    public string GenerateKey()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
