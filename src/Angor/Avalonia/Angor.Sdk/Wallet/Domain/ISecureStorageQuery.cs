using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Domain;

/// <summary>
/// Query interface for retrieving data from secure storage.
/// Separates read operations from write operations (CQRS pattern).
/// </summary>
public interface ISecureStorageQuery
{
    /// <summary>
    /// Retrieves the master encryption key for a wallet from secure storage.
    /// The key is stored using Windows DPAPI for protection.
    /// </summary>
    Task<Result<string>> GetMasterKeyAsync(
        string walletId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a master key exists for a wallet in secure storage.
    /// </summary>
    Task<Result<bool>> HasMasterKeyAsync(
        string walletId,
        CancellationToken cancellationToken = default
    );
}

