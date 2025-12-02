using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Angor.Contexts.CrossCutting;

namespace Angor.Contexts.Integration.WalletFunding.KeyStore;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class LinuxWalletEncryptionKeyStore : IEncryptionKeyStore
{
    private readonly string _storeFilePath;

    public LinuxWalletEncryptionKeyStore()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(home, ".config", "angor");
        Directory.CreateDirectory(configDir);
        _storeFilePath = Path.Combine(configDir, "wallet_keys.json");
    }

    private static string BuildKey(WalletId walletId) => walletId.Value;

    public async Task<string?> GetKeyAsync(WalletId walletId)
    {
        var map = await LoadAsync();
        if (!map.TryGetValue(BuildKey(walletId), out var base64))
            return null;

        var protectedBytes = Convert.FromBase64String(base64);
        var unprotected = ProtectedData.Unprotect(
            protectedBytes,
            null,
            DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(unprotected);
    }

    public async Task SaveKeyAsync(WalletId walletId, string encryptionKey)
    {
        var map = await LoadAsync();

        var bytes = Encoding.UTF8.GetBytes(encryptionKey);
        var protectedBytes = ProtectedData.Protect(
            bytes,
            null,
            DataProtectionScope.CurrentUser);

        map[BuildKey(walletId)] = Convert.ToBase64String(protectedBytes);
        await SaveAsync(map);
    }

    public async Task DeleteKeyAsync(WalletId walletId)
    {
        var map = await LoadAsync();
        if (map.Remove(BuildKey(walletId)))
            await SaveAsync(map);
    }

    private async Task<Dictionary<string, string>> LoadAsync()
    {
        if (!File.Exists(_storeFilePath))
            return new Dictionary<string, string>();

        await using var stream = File.OpenRead(_storeFilePath);
        var map = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream);
        return map ?? new Dictionary<string, string>();
    }

    private async Task SaveAsync(Dictionary<string, string> map)
    {
        await using var stream = File.Create(_storeFilePath);
        await JsonSerializer.SerializeAsync(stream, map, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
