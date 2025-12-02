#if IOS

using Angor.Contexts.CrossCutting;

using System.Text;
using Security;

public class IosWalletEncryptionKeyStore : IWalletEncryptionKeyStore
{
    private static string BuildKey(WalletId walletId) => $"wallet_encryption_key_{walletId.Value}";

    public Task<string?> GetKeyAsync(WalletId walletId)
    {
        var query = new SecRecord(SecKind.GenericPassword)
        {
            Service = BuildKey(walletId),
            Account = "default"
        };

        var result = SecKeyChain.QueryAsRecord(query, out var status);
        if (status != SecStatusCode.Success || result?.ValueData is null)
            return Task.FromResult<string?>(null);

        var value = Encoding.UTF8.GetString(result.ValueData.ToArray());
        return Task.FromResult<string?>(value);
    }

    public Task SaveKeyAsync(WalletId walletId, string encryptionKey)
    {
        var key = BuildKey(walletId);

        // delete existing
        var existing = new SecRecord(SecKind.GenericPassword)
        {
            Service = key,
            Account = "default"
        };
        SecKeyChain.Remove(existing);

        var data = NSData.FromArray(Encoding.UTF8.GetBytes(encryptionKey));
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

    public Task DeleteKeyAsync(WalletId walletId)
    {
        var record = new SecRecord(SecKind.GenericPassword)
        {
            Service = BuildKey(walletId),
            Account = "default"
        };
        SecKeyChain.Remove(record);
        return Task.CompletedTask;
    }
}
#endif