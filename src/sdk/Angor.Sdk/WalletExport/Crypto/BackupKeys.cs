using System.Security.Cryptography;
using System.Text;
using NBitcoin.Secp256k1;

namespace Angor.Sdk.WalletExport.Crypto;

/// <summary>
/// Materialised secrets derived from a recovery passphrase.
/// Holds zero-on-dispose buffers — use within a <c>using</c> block.
/// </summary>
internal sealed class BackupKeys : IDisposable
{
    public byte[] MasterSeed { get; }
    public byte[] BackupPrivateKey { get; }
    public byte[] InnerAeadKey { get; }
    public string BackupPrivateKeyHex { get; }
    public string BackupPublicKeyHex { get; }

    private bool disposed;

    private BackupKeys(byte[] masterSeed, byte[] backupPrivateKey, byte[] innerAeadKey,
        string backupPrivateKeyHex, string backupPublicKeyHex)
    {
        MasterSeed = masterSeed;
        BackupPrivateKey = backupPrivateKey;
        InnerAeadKey = innerAeadKey;
        BackupPrivateKeyHex = backupPrivateKeyHex;
        BackupPublicKeyHex = backupPublicKeyHex;
    }

    /// <summary>
    /// Derives all backup secrets from a recovery passphrase.
    /// Returns an instance whose buffers MUST be disposed.
    /// </summary>
    public static BackupKeys FromPassphrase(string passphrase)
    {
        byte[] master = Argon2idKdf.Derive(passphrase);
        return FromMasterSeed(master);
    }

    /// <summary>
    /// Derives all backup secrets from a pre-computed Argon2id master seed.
    /// Takes ownership of the supplied buffer (caller must not zero it).
    /// </summary>
    public static BackupKeys FromMasterSeed(byte[] masterSeed)
    {
        if (masterSeed is null || masterSeed.Length != 32)
            throw new ArgumentException("Master seed must be 32 bytes.", nameof(masterSeed));

        byte[] backupPrivateKey = DeriveSecp256k1Key(masterSeed, "angor-backup-identity-v1");
        byte[] innerAeadKey = HkdfExpand(masterSeed, "angor-backup-aead-v1", 32);

        if (!Context.Instance.TryCreateECPrivKey(backupPrivateKey, out var privKey) || privKey is null)
            throw new CryptographicException("Derived private key invalid for secp256k1.");

        try
        {
            var xOnly = privKey.CreateXOnlyPubKey();
            var pubKeyBytes = new byte[32];
            xOnly.WriteToSpan(pubKeyBytes);

            return new BackupKeys(
                masterSeed,
                backupPrivateKey,
                innerAeadKey,
                Convert.ToHexString(backupPrivateKey).ToLowerInvariant(),
                Convert.ToHexString(pubKeyBytes).ToLowerInvariant());
        }
        finally
        {
            privKey.Dispose();
        }
    }

    /// <summary>
    /// Derive a valid secp256k1 private key from the master seed using HKDF-Expand with
    /// a counter byte. On the (vanishingly rare) chance of a 0 or out-of-range output, the
    /// counter is incremented and HKDF re-run.
    /// </summary>
    private static byte[] DeriveSecp256k1Key(byte[] masterSeed, string info)
    {
        for (byte counter = 0; counter < byte.MaxValue; counter++)
        {
            var infoBytes = Encoding.UTF8.GetBytes(info + "-" + counter);
            var candidate = HKDF.Expand(HashAlgorithmName.SHA256, masterSeed, 32, infoBytes);
            if (Context.Instance.TryCreateECPrivKey(candidate, out var key) && key is not null)
            {
                key.Dispose();
                return candidate;
            }
        }
        throw new CryptographicException("Failed to derive a valid secp256k1 key.");
    }

    private static byte[] HkdfExpand(byte[] ikm, string info, int length)
    {
        return HKDF.Expand(HashAlgorithmName.SHA256, ikm, length, Encoding.UTF8.GetBytes(info));
    }

    public void Dispose()
    {
        if (disposed) return;
        CryptographicOperations.ZeroMemory(MasterSeed);
        CryptographicOperations.ZeroMemory(BackupPrivateKey);
        CryptographicOperations.ZeroMemory(InnerAeadKey);
        disposed = true;
    }
}
