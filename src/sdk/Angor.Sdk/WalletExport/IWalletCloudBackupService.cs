using Angor.Sdk.Common;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.WalletExport;

/// <summary>
/// Wallet-aware cloud backup operations.
/// Wires <see cref="ICloudBackupService"/> together with the wallet store so the per-wallet backup
/// state is persisted to <c>wallets.json</c>.
/// </summary>
public interface IWalletCloudBackupService
{
    Task<Result<Maybe<CloudBackupRecord>>> GetStatus(WalletId walletId);

    Task<Result<BackupCreationResult>> EnableAsync(
        WalletId walletId,
        string recoveryPassphrase,
        string label,
        CancellationToken cancellationToken = default);

    Task<Result> DisableAsync(WalletId walletId);

    /// <summary>
    /// Re-derive keys from the passphrase and re-publish the manifest + (if necessary) re-upload the blob.
    /// User-triggered: requires the passphrase. The blob hash never changes for the same payload, so
    /// re-uploads land at the same Blossom address.
    /// </summary>
    Task<Result<BackupCreationResult>> RefreshAsync(
        WalletId walletId,
        string recoveryPassphrase,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Passive health probe — HEADs the cached Blossom URLs to update per-server availability.
    /// Does NOT require the passphrase. Cannot self-repair missing blobs (that needs the BUD-02 auth key).
    /// </summary>
    Task<Result<BackupHealthResult>> VerifyHealthAsync(
        WalletId walletId,
        CancellationToken cancellationToken = default);
}
