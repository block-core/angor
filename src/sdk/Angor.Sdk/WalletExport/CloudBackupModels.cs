namespace Angor.Sdk.WalletExport;

/// <summary>
/// Snapshot of where an active cloud backup is stored. Cached in the wallet record so the
/// background health service can refresh without re-deriving keys from the passphrase.
/// </summary>
public sealed class CloudBackupRecord
{
    /// <summary>The backup-identity Schnorr public key (hex, x-only). Doubles as the relay author for the manifest event.</summary>
    public string BackupPubKeyHex { get; set; } = string.Empty;

    /// <summary>Lower-case hex SHA-256 of the encrypted blob — the Blossom content address.</summary>
    public string BlobSha256 { get; set; } = string.Empty;

    /// <summary>Servers we believe currently hold the blob.</summary>
    public List<string> Servers { get; set; } = new();

    /// <summary>NIP-44 outer ciphertext of the manifest. Stored so refresh-on-launch can re-publish without the passphrase.</summary>
    public string ManifestCipherText { get; set; } = string.Empty;

    /// <summary>Unix seconds when the backup was first created.</summary>
    public long CreatedAtUnix { get; set; }

    /// <summary>Unix seconds of the last fully successful relay+blob verification.</summary>
    public long? LastVerifiedAtUnix { get; set; }

    /// <summary>Per-server health: server URL → true if the blob was reachable last time we checked.</summary>
    public Dictionary<string, bool> ServerHealth { get; set; } = new();
}

/// <summary>
/// Outcome of a backup setup attempt.
/// </summary>
public sealed record BackupCreationResult(
    string BackupPubKeyHex,
    string BlobSha256,
    IReadOnlyList<string> UploadedToServers,
    IReadOnlyList<string> FailedServers,
    long CreatedAtUnix);

/// <summary>
/// Outcome of a recovery attempt — the decrypted seed payload ready to feed into the wallet restore flow.
/// </summary>
public sealed record BackupRecoveryResult(BackupSeedPayload Payload, string BlobServedFrom);

/// <summary>
/// Outcome of a background refresh / health check cycle.
/// </summary>
public sealed record BackupHealthResult(
    bool RelayManifestPublished,
    int ServersReachable,
    int ServersChecked,
    IReadOnlyList<string> RehealedServers,
    long CheckedAtUnix);
