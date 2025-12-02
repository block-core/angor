using Angor.Contexts.CrossCutting;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

public class WindowsWalletEncryptionKeyStore : IEncryptionKeyStore
{
    private const string RegistryBasePath = @"Software\Angor\WalletKeys";

    private static string BuildKey(WalletId walletId) => walletId.Value;

    public Task<string?> GetKeyAsync(WalletId walletId)
    {
        using var keyRoot = Registry.CurrentUser.OpenSubKey(RegistryBasePath);
        if (keyRoot is null)
            return Task.FromResult<string?>(null);

        var protectedBytes = keyRoot.GetValue(BuildKey(walletId)) as byte[];
        if (protectedBytes is null)
            return Task.FromResult<string?>(null);

        var unprotected = ProtectedData.Unprotect(
            protectedBytes,
            null,
            DataProtectionScope.CurrentUser);

        var value = Encoding.UTF8.GetString(unprotected);
        return Task.FromResult<string?>(value);
    }

    public Task SaveKeyAsync(WalletId walletId, string encryptionKey)
    {
        using var keyRoot = Registry.CurrentUser.CreateSubKey(RegistryBasePath)!;

        var bytes = Encoding.UTF8.GetBytes(encryptionKey);
        var protectedBytes = ProtectedData.Protect(
            bytes,
            null,
            DataProtectionScope.CurrentUser);

        keyRoot.SetValue(BuildKey(walletId), protectedBytes, RegistryValueKind.Binary);
        return Task.CompletedTask;
    }

    public Task DeleteKeyAsync(WalletId walletId)
    {
        using var keyRoot = Registry.CurrentUser.OpenSubKey(RegistryBasePath, writable: true);
        keyRoot?.DeleteValue(BuildKey(walletId), throwOnMissingValue: false);
        return Task.CompletedTask;
    }
}