using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Domain;

/// <summary>
/// Command interface for managing master encryption keys in secure storage.
/// Uses Windows DPAPI to protect the keys.
/// Master keys are used to encrypt/decrypt wallet seed words with AES-GCM.
/// </summary>
public interface ISecureStorageCommand
{
    /// <summary>
    /// Stores a master encryption key securely using Windows DPAPI.
    /// Generates a new master key if not provided.
    /// Returns the stored key for reference.
    /// </summary>
    Task<Result<string>> StoreMasterKeyAsync(
        string walletId,
        string? masterKey = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Deletes the master encryption key for a wallet from secure storage.
    /// This should be called when a wallet is deleted.
    /// </summary>
    Task<Result> DeleteMasterKeyAsync(
        string walletId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates the master encryption key for a wallet.
    /// Useful for key rotation scenarios.
    /// </summary>
    Task<Result<string>> UpdateMasterKeyAsync(
        string walletId,
        string newMasterKey,
        CancellationToken cancellationToken = default
    );
}

public interface ISecureStorage: ISecureStorageCommand, ISecureStorageQuery{}