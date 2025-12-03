namespace Angor.Contexts.CrossCutting;

/// <summary>
/// Stores auto-generated wallet passwords securely using platform-specific mechanisms.
/// This is separate from IEncryptionKeyStore to handle automatic password generation.
/// </summary>
public interface IAutoPasswordStore
{
    /// <summary>
    /// Gets an existing password or generates and stores a new one if it doesn't exist.
    /// </summary>
    Task<string> GetOrCreatePasswordAsync(WalletId walletId);
    
    /// <summary>
    /// Gets an existing password without creating a new one.
    /// </summary>
    Task<string?> GetPasswordAsync(WalletId walletId);
    
    /// <summary>
    /// Deletes the stored password for a wallet.
    /// </summary>
    Task DeletePasswordAsync(WalletId walletId);
}

