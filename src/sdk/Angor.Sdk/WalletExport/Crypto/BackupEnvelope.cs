using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nostr.Client.Keys;
using Nostr.Client.Utils;

namespace Angor.Sdk.WalletExport.Crypto;

/// <summary>
/// Two-layer envelope:
///   inner = AES-256-GCM(K_inner) over the seed JSON payload
///   outer = NIP-44 v2(self-ECDH backup_sk × backup_pk) over the manifest JSON
/// </summary>
internal static class BackupEnvelope
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly byte[] InnerAad = Encoding.UTF8.GetBytes(BackupManifest.DTag);

    /// <summary>
    /// Serialise + AEAD-encrypt the seed payload. Returns the raw byte buffer (nonce ‖ tag ‖ ct).
    /// </summary>
    public static byte[] EncryptInner(BackupSeedPayload payload, BackupKeys keys)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(keys);

        var plaintextBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        try
        {
            return AeadCipher.Encrypt(keys.InnerAeadKey, plaintextBytes, InnerAad);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    /// <summary>
    /// AEAD-decrypt + deserialise into the seed payload. Throws on tag mismatch.
    /// </summary>
    public static BackupSeedPayload DecryptInner(byte[] cipherBuffer, BackupKeys keys)
    {
        ArgumentNullException.ThrowIfNull(cipherBuffer);
        ArgumentNullException.ThrowIfNull(keys);

        var plaintextBytes = AeadCipher.Decrypt(keys.InnerAeadKey, cipherBuffer, InnerAad);
        try
        {
            var payload = JsonSerializer.Deserialize<BackupSeedPayload>(plaintextBytes, JsonOptions)
                          ?? throw new CryptographicException("Decrypted payload was empty.");
            return payload;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    /// <summary>
    /// NIP-44 v2 self-encryption of the manifest JSON. Returns the base64 payload that
    /// goes verbatim into the kind 30078 event content.
    /// </summary>
    public static string EncryptOuterManifest(BackupManifest manifest, BackupKeys keys)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(keys);

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        var conversationKey = DeriveSelfConversationKey(keys);
        return NostrEncryptionNip44.Encrypt(json, conversationKey);
    }

    /// <summary>
    /// NIP-44 v2 decryption of a kind 30078 event content. Throws on tamper / wrong key.
    /// </summary>
    public static BackupManifest DecryptOuterManifest(string outerCipher, BackupKeys keys)
    {
        if (string.IsNullOrWhiteSpace(outerCipher))
            throw new ArgumentException("Outer cipher is empty.", nameof(outerCipher));
        ArgumentNullException.ThrowIfNull(keys);

        var conversationKey = DeriveSelfConversationKey(keys);
        var json = NostrEncryptionNip44.Decrypt(outerCipher, conversationKey);
        return JsonSerializer.Deserialize<BackupManifest>(json, JsonOptions)
               ?? throw new CryptographicException("Decrypted manifest was empty.");
    }

    private static byte[] DeriveSelfConversationKey(BackupKeys keys)
    {
        // NIP-44 v2: conversation_key = HKDF(ECDH_x_coord, salt="nip44-v2")
        // Self-ECDH means sender and recipient are the same identity; the conversation key
        // is still well-defined (sk·sk·G is a valid curve point).
        var nsec = NostrPrivateKey.FromHex(keys.BackupPrivateKeyHex);
        var npub = NostrPublicKey.FromHex(keys.BackupPublicKeyHex);
        return nsec.DeriveConversationKeyNip44(npub);
    }
}
