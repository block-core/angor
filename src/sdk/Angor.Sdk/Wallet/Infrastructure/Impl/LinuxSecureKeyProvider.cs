using System.Security.Cryptography;
using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

/// <summary>
/// Linux ISecureKeyProvider that stores wallet encryption keys in a file with
/// owner-only permissions (chmod 600). A PIN-based UI layer can be added later
/// for additional protection.
/// </summary>
public class LinuxSecureKeyProvider : ISecureKeyProvider
{
    private readonly string _filePath;

    public LinuxSecureKeyProvider(IApplicationStorage storage, ProfileContext profileContext)
    {
        var directory = storage.GetProfileDirectory(profileContext.AppName, profileContext.ProfileName);
        _filePath = Path.Combine(directory, "wallet-keys.json");
        EnsureRestrictedPermissions();
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

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    private void SaveKeys(Dictionary<string, string> keys)
    {
        var json = JsonSerializer.Serialize(keys);
        File.WriteAllText(_filePath, json);
        EnsureRestrictedPermissions();
    }

    private void EnsureRestrictedPermissions()
    {
        if (!File.Exists(_filePath))
            return;

        File.SetUnixFileMode(_filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
