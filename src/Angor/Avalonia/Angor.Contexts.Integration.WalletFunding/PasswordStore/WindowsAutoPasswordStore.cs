using System.Security.Cryptography;
using System.Text;
using Angor.Contexts.CrossCutting;
using Microsoft.Win32;

namespace Angor.Contexts.Integration.WalletFunding.PasswordStore;

public class WindowsAutoPasswordStore : IAutoPasswordStore
{
    private const string RegistryBasePath = @"Software\Angor\AutoPasswords";

    public async Task<string> GetOrCreatePasswordAsync(WalletId walletId)
    {
        var existing = await GetPasswordAsync(walletId);
        if (existing != null)
            return existing;

        var newPassword = GeneratePassword();
        await SavePasswordAsync(walletId, newPassword);
        return newPassword;
    }

    public Task<string?> GetPasswordAsync(WalletId walletId)
    {
        using var keyRoot = Registry.CurrentUser.OpenSubKey(RegistryBasePath);
        if (keyRoot is null)
            return Task.FromResult<string?>(null);

        var protectedBytes = keyRoot.GetValue(walletId.Value) as byte[];
        if (protectedBytes is null)
            return Task.FromResult<string?>(null);

        var unprotected = ProtectedData.Unprotect(
            protectedBytes,
            null,
            DataProtectionScope.CurrentUser);

        var value = Encoding.UTF8.GetString(unprotected);
        return Task.FromResult<string?>(value);
    }

    public Task DeletePasswordAsync(WalletId walletId)
    {
        using var keyRoot = Registry.CurrentUser.OpenSubKey(RegistryBasePath, writable: true);
        keyRoot?.DeleteValue(walletId.Value, throwOnMissingValue: false);
        return Task.CompletedTask;
    }

    private Task SavePasswordAsync(WalletId walletId, string password)
    {
        using var keyRoot = Registry.CurrentUser.CreateSubKey(RegistryBasePath)!;

        var bytes = Encoding.UTF8.GetBytes(password);
        var protectedBytes = ProtectedData.Protect(
            bytes,
            null,
            DataProtectionScope.CurrentUser);

        keyRoot.SetValue(walletId.Value, protectedBytes, RegistryValueKind.Binary);
        return Task.CompletedTask;
    }

    private static string GeneratePassword()
    {
        const int keySizeBytes = 32; // 256 bits
        var bytes = RandomNumberGenerator.GetBytes(keySizeBytes);
        return Convert.ToBase64String(bytes);
    }
}

