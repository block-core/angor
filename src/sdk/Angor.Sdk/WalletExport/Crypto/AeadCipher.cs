using System.Security.Cryptography;

namespace Angor.Sdk.WalletExport.Crypto;

/// <summary>
/// AES-256-GCM with key supplied directly (no KDF).
/// Wire format: nonce (12 bytes) ‖ tag (16 bytes) ‖ ciphertext.
/// </summary>
internal static class AeadCipher
{
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int KeySize = 32;

    /// <summary>
    /// Encrypts plaintext with a fresh random nonce.
    /// Returns the concatenated buffer (nonce ‖ tag ‖ ciphertext).
    /// </summary>
    public static byte[] Encrypt(byte[] key32, byte[] plaintext, byte[]? associatedData = null)
    {
        if (key32 is null || key32.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(key32));
        ArgumentNullException.ThrowIfNull(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];

        using (var aes = new AesGcm(key32, TagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        }

        var output = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, output, NonceSize + TagSize, ciphertext.Length);
        return output;
    }

    /// <summary>
    /// Decrypts a buffer produced by <see cref="Encrypt"/>.
    /// Throws <see cref="CryptographicException"/> on tag mismatch or malformed input.
    /// </summary>
    public static byte[] Decrypt(byte[] key32, byte[] buffer, byte[]? associatedData = null)
    {
        if (key32 is null || key32.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(key32));
        ArgumentNullException.ThrowIfNull(buffer);
        if (buffer.Length < NonceSize + TagSize)
            throw new CryptographicException("Ciphertext too short.");

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertext = new byte[buffer.Length - NonceSize - TagSize];

        Buffer.BlockCopy(buffer, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(buffer, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(buffer, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key32, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
        return plaintext;
    }
}
