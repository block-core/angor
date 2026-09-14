using CSharpFunctionalExtensions;

namespace Angor.Sdk.WalletExport;

/// <summary>
/// Recover a wallet seed from a previously-published cloud backup.
/// Recovery requires only the recovery passphrase — no device, account, or network identity.
/// </summary>
public interface IBackupRecoveryService
{
    /// <summary>
    /// Derive the backup identity from the passphrase, query relays for the manifest, fetch the
    /// encrypted blob from any healthy Blossom server, and decrypt the seed.
    /// </summary>
    Task<Result<BackupRecoveryResult>> RecoverAsync(string recoveryPassphrase, CancellationToken cancellationToken = default);
}
