using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Blockcore.NBitcoin.BIP32;

namespace Angor.Shared.Models;

/// <summary>
/// Holds BIP-39 mnemonic words and optional passphrase.
/// Backed by char[] so the raw material can be zeroed on disposal.
/// Caches the derived ExtKey to avoid repeated PBKDF2 derivations.
/// </summary>
public class WalletWords : IDisposable
{
    private char[] wordsChars = Array.Empty<char>();
    private char[]? passphraseChars;
    private ExtKey? cachedExtKey;
    private bool disposed;

    /// <summary>The BIP-39 mnemonic words (space-separated).</summary>
    public string Words
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return new string(wordsChars);
        }
        set => wordsChars = value?.ToCharArray() ?? Array.Empty<char>();
    }

    /// <summary>Optional BIP-39 passphrase.</summary>
    public string? Passphrase
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return passphraseChars != null ? new string(passphraseChars) : null;
        }
        set => passphraseChars = value?.ToCharArray();
    }

    /// <summary>
    /// Gets or derives the master extended key, caching it for subsequent calls.
    /// Avoids repeated PBKDF2 derivation from the mnemonic on every signing operation.
    /// </summary>
    [JsonIgnore]
    public ExtKey? CachedExtKey => cachedExtKey;

    /// <summary>
    /// Derives and caches the master ExtKey from the mnemonic.
    /// Subsequent calls return the cached key without touching the mnemonic.
    /// </summary>
    public ExtKey GetOrDeriveExtKey(IHdOperations hdOperations)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (cachedExtKey != null)
            return cachedExtKey;

        cachedExtKey = hdOperations.GetExtendedKey(Words, Passphrase);
        return cachedExtKey;
    }

    public string ConvertToString()
    {
        return JsonSerializer.Serialize(this);
    }

    public static WalletWords ConvertFromString(string data)
    {
        if (string.IsNullOrEmpty(data))
            throw new InvalidOperationException();

        return JsonSerializer.Deserialize<WalletWords>(data) ?? throw new InvalidOperationException();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(wordsChars.AsSpan()));

        if (passphraseChars != null)
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(passphraseChars.AsSpan()));

        cachedExtKey = null;
    }
}
