using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Angor.Contexts.CrossCutting;

namespace Angor.Contexts.Integration.WalletFunding.PasswordStore;

public class LinuxAutoPasswordStore : IAutoPasswordStore
{
    private readonly string _storeFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LinuxAutoPasswordStore()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(home, ".config", "angor");
        Directory.CreateDirectory(configDir);
        _storeFilePath = Path.Combine(configDir, "auto_passwords.json");
        
        // Set restrictive file permissions (600) if file exists
        if (File.Exists(_storeFilePath) && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_storeFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public async Task<string> GetOrCreatePasswordAsync(WalletId walletId)
    {
        var existing = await GetPasswordAsync(walletId);
        if (existing != null)
            return existing;

        var newPassword = GeneratePassword();
        await SavePasswordAsync(walletId, newPassword);
        return newPassword;
    }

    public async Task<string?> GetPasswordAsync(WalletId walletId)
    {
        await _lock.WaitAsync();
        try
        {
            var map = await LoadAsync();
            if (!map.TryGetValue(walletId.Value, out var base64))
                return null;

            var protectedBytes = Convert.FromBase64String(base64);
            var unprotected = ProtectedData.Unprotect(
                protectedBytes,
                null,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(unprotected);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeletePasswordAsync(WalletId walletId)
    {
        await _lock.WaitAsync();
        try
        {
            var map = await LoadAsync();
            if (map.Remove(walletId.Value))
                await SaveAsync(map);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SavePasswordAsync(WalletId walletId, string password)
    {
        await _lock.WaitAsync();
        try
        {
            var map = await LoadAsync();

            var bytes = Encoding.UTF8.GetBytes(password);
            var protectedBytes = ProtectedData.Protect(
                bytes,
                null,
                DataProtectionScope.CurrentUser);

            map[walletId.Value] = Convert.ToBase64String(protectedBytes);
            await SaveAsync(map);
        }
        finally
        {
            _lock.Release();
        }
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
        
        // Set restrictive permissions after creation
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_storeFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static string GeneratePassword()
    {
        const int keySizeBytes = 32; // 256 bits
        var bytes = RandomNumberGenerator.GetBytes(keySizeBytes);
        return Convert.ToBase64String(bytes);
    }
}

