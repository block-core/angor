using System.Text.Json.Serialization;

namespace Angor.Sdk.WalletExport;

/// <summary>
/// Plaintext seed JSON before inner AEAD encryption. Lives only inside the encrypted blob.
/// </summary>
public sealed class BackupSeedPayload
{
    [JsonPropertyName("v")] public int Version { get; set; } = 1;
    [JsonPropertyName("wallet_id")] public string WalletId { get; set; } = string.Empty;
    [JsonPropertyName("network")] public string Network { get; set; } = string.Empty;
    [JsonPropertyName("mnemonic")] public string Mnemonic { get; set; } = string.Empty;
    [JsonPropertyName("bip39_passphrase")] public string Bip39Passphrase { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("created_at")] public long CreatedAtUnix { get; set; }
}

/// <summary>
/// Plaintext manifest JSON before outer NIP-44 wrapping. Carries Blossom blob coordinates
/// (SHA-256 + server URLs) so recovery can locate and verify the encrypted blob.
/// </summary>
public sealed class BackupManifest
{
    public const string DTag = "angor-seed-backup-v1";
    public const string CurrentAlgorithm = "argon2id-v13+aes256gcm+nip44v2";

    [JsonPropertyName("v")] public int Version { get; set; } = 1;
    [JsonPropertyName("algo")] public string Algorithm { get; set; } = CurrentAlgorithm;
    [JsonPropertyName("blob_sha256")] public string BlobSha256 { get; set; } = string.Empty;
    [JsonPropertyName("blob_size")] public long BlobSize { get; set; }
    [JsonPropertyName("servers")] public List<string> Servers { get; set; } = new();
    [JsonPropertyName("created_at")] public long CreatedAtUnix { get; set; }
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
}
