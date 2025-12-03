#if IOS
using System.Security.Cryptography;
using System.Text;
using Angor.Contexts.CrossCutting;
using Security;

namespace Angor.Contexts.Integration.WalletFunding.PasswordStore;

public class IosAutoPasswordStore : IAutoPasswordStore
{
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
        var query = new SecRecord(SecKind.GenericPassword)
        {
            Service = $"angor_auto_pwd_{walletId.Value}",
            Account = "default"
        };

        var result = SecKeyChain.QueryAsRecord(query, out var status);
        if (status != SecStatusCode.Success || result?.ValueData is null)
            return Task.FromResult<string?>(null);

        var value = Encoding.UTF8.GetString(result.ValueData.ToArray());
        return Task.FromResult<string?>(value);
    }

    public Task DeletePasswordAsync(WalletId walletId)
    {
        var record = new SecRecord(SecKind.GenericPassword)
        {
            Service = $"angor_auto_pwd_{walletId.Value}",
            Account = "default"
        };
        SecKeyChain.Remove(record);
        return Task.CompletedTask;
    }

    private Task SavePasswordAsync(WalletId walletId, string password)
    {
        var key = $"angor_auto_pwd_{walletId.Value}";

        // Delete existing
        var existing = new SecRecord(SecKind.GenericPassword)
        {
            Service = key,
            Account = "default"
        };
        SecKeyChain.Remove(existing);

        var data = NSData.FromArray(Encoding.UTF8.GetBytes(password));
        var record = new SecRecord(SecKind.GenericPassword)
        {
            Service = key,
            Account = "default",
            ValueData = data
        };

        var status = SecKeyChain.Add(record);
        if (status != SecStatusCode.Success)
            throw new InvalidOperationException($"Keychain save failed: {status}");

        return Task.CompletedTask;
    }

    private static string GeneratePassword()
    {
        const int keySizeBytes = 32; // 256 bits
        var bytes = RandomNumberGenerator.GetBytes(keySizeBytes);
        return Convert.ToBase64String(bytes);
    }
}
#endif

