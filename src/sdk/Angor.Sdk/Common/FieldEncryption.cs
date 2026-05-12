using System.Security.Cryptography;
using System.Text;

namespace Angor.Sdk.Common;

/// <summary>
/// Lightweight AES-GCM encryption for individual string fields.
/// Uses the raw key directly (no key derivation) since keys from ISecureKeyProvider are already cryptographically random.
/// </summary>
public static class FieldEncryption
{
    private const int IvSize = 12;
    private const int TagSize = 16;

    public static string Encrypt(string plaintext, string base64Key)
    {
        var key = Convert.FromBase64String(base64Key);
        var iv = RandomNumberGenerator.GetBytes(IvSize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(iv, plaintextBytes, ciphertext, tag);

        // Format: iv + ciphertext + tag
        var result = new byte[IvSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(iv, 0, result, 0, IvSize);
        Buffer.BlockCopy(ciphertext, 0, result, IvSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, IvSize + ciphertext.Length, TagSize);

        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string encryptedBase64, string base64Key)
    {
        var key = Convert.FromBase64String(base64Key);
        var encryptedBytes = Convert.FromBase64String(encryptedBase64);

        var iv = new byte[IvSize];
        var ciphertext = new byte[encryptedBytes.Length - IvSize - TagSize];
        var tag = new byte[TagSize];

        Buffer.BlockCopy(encryptedBytes, 0, iv, 0, IvSize);
        Buffer.BlockCopy(encryptedBytes, IvSize, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(encryptedBytes, IvSize + ciphertext.Length, tag, 0, TagSize);

        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(iv, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}