using CSharpFunctionalExtensions;

namespace Angor.Sdk.WalletExport;

/// <summary>
/// Set up and tear down a cloud backup of the wallet seed.
/// The recovery passphrase never leaves this layer — it is consumed to derive keys, then zeroed.
/// </summary>
public interface ICloudBackupService
{
    /// <summary>
    /// Encrypt the seed payload, upload it to multiple Blossom servers, and publish a kind 30078
    /// manifest event so the backup is discoverable by passphrase alone.
    /// </summary>
    /// <param name="recoveryPassphrase">User-chosen passphrase. Stretched with Argon2id. Zeroed after use.</param>
    /// <param name="payload">Plaintext seed payload (mnemonic + network + optional BIP-39 passphrase + label).</param>
    /// <param name="minServerSuccessThreshold">Minimum number of Blossom uploads required to consider the backup published.</param>
    Task<Result<(BackupCreationResult Result, CloudBackupRecord Record)>> CreateBackupAsync(
        string recoveryPassphrase,
        BackupSeedPayload payload,
        int minServerSuccessThreshold = 2,
        CancellationToken cancellationToken = default);
}
