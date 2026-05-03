using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

/// <summary>
/// Windows-only ISecureKeyProvider that protects wallet encryption keys using DPAPI
/// (DataProtectionScope.CurrentUser). Keys are encrypted at rest and tied to the
/// Windows user profile.
/// </summary>
public class DpapiSecureKeyProvider : ISecureKeyProvider
{
    private readonly string _filePath;

    public DpapiSecureKeyProvider(IApplicationStorage storage, ProfileContext profileContext)
    {
        var directory = storage.GetProfileDirectory(profileContext.AppName, profileContext.ProfileName);
        _filePath = Path.Combine(directory, "wallet-keys.dpapi");
    }

    public Task<Maybe<string>> Get(WalletId walletId)
    {
        var keys = LoadKeys();
        return Task.FromResult(keys.TryGetValue(walletId.Value, out var key)
            ? Maybe<string>.From(key)
            : Maybe<string>.None);
    }

    public Task Save(WalletId walletId, string key)
    {
        var keys = LoadKeys();
        keys[walletId.Value] = key;
        SaveKeys(keys);
        return Task.CompletedTask;
    }

    public Task Remove(WalletId walletId)
    {
        var keys = LoadKeys();
        if (keys.Remove(walletId.Value))
        {
            SaveKeys(keys);
        }
        return Task.CompletedTask;
    }

    public string GenerateKey()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private Dictionary<string, string> LoadKeys()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, string>();

        var encrypted = File.ReadAllBytes(_filePath);
        if (encrypted.Length == 0)
            return new Dictionary<string, string>();

        var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        var json = Encoding.UTF8.GetString(decrypted);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    private void SaveKeys(Dictionary<string, string> keys)
    {
        var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(keys));
        var encrypted = ProtectedData.Protect(json, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, encrypted);
    }
}
