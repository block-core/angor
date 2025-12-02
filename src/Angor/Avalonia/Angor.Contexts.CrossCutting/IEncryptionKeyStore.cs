namespace Angor.Contexts.CrossCutting;

public interface IEncryptionKeyStore
{
    Task<string?> GetKeyAsync(WalletId walletId);
    Task SaveKeyAsync(WalletId walletId, string encryptionKey);
    Task DeleteKeyAsync(WalletId walletId);
}