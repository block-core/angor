using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Angor.Sdk.WalletExport.Crypto;

/// <summary>
/// Argon2id key derivation tuned for the cloud-backup recovery passphrase.
/// Parameters follow OWASP 2024 recommendations: m=64 MiB, t=3, p=1, 32-byte output.
/// </summary>
internal static class Argon2idKdf
{
    public const int MemoryKb = 65_536;
    public const int Iterations = 3;
    public const int Parallelism = 1;
    public const int OutputLength = 32;

    public const string DomainSeparationLabel = "angor-seed-backup-v1";

    /// <summary>
    /// Derives a 32-byte master seed from a recovery passphrase.
    /// The passphrase is NFC-normalised and encoded as UTF-8 before hashing.
    /// The salt is deterministic — Argon2id memory cost is what blocks rainbow tables.
    /// </summary>
    public static byte[] Derive(string passphrase)
    {
        if (string.IsNullOrEmpty(passphrase))
            throw new ArgumentException("Passphrase must not be empty.", nameof(passphrase));

        var normalised = passphrase.Normalize(NormalizationForm.FormC);
        var passwordBytes = Encoding.UTF8.GetBytes(normalised);

        try
        {
            return Derive(passwordBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    /// <summary>
    /// Derives a 32-byte master seed from raw passphrase bytes. The caller is
    /// responsible for zeroing the input buffer after use.
    /// </summary>
    public static byte[] Derive(byte[] passwordBytes)
    {
        ArgumentNullException.ThrowIfNull(passwordBytes);

        var salt = DeriveSalt();
        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            MemorySize = MemoryKb,
            Iterations = Iterations
        };

        return argon2.GetBytes(OutputLength);
    }

    private static byte[] DeriveSalt()
    {
        // 16-byte deterministic salt = first 16 bytes of SHA-256("angor-seed-backup-v1")
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(DomainSeparationLabel));
        var salt = new byte[16];
        Buffer.BlockCopy(hash, 0, salt, 0, 16);
        return salt;
    }
}
