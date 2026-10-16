using Angor.Sdk.WalletExport;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class EncryptedWallet
{
    public string Id { get; set; }
    public string EncryptedData { get; set; }
    public string Salt { get; set; }
    public string IV { get; set; }

    /// <summary>
    /// Optional cloud-backup metadata. Populated when the user enables backup; null otherwise.
    /// Does NOT contain the seed, the recovery passphrase, or any AEAD key — only public coordinates
    /// (backup pubkey, blob SHA-256, server list) and the NIP-44 outer ciphertext so the background
    /// health service can re-publish without the passphrase.
    /// </summary>
    public CloudBackupRecord? CloudBackup { get; set; }
}
