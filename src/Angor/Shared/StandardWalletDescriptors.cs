using Angor.Shared.Models;

namespace Angor.Shared;

/// <summary>
/// Helpers for BIP-84 (wpkh) standard wallet descriptor generation and parsing.
/// Phase 1 supports wpkh (P2WPKH / native SegWit) only.
///
/// Descriptor format:
///   Receive: wpkh([fingerprint/84h/coinTypeh/0h]xpub.../0/*)
///   Change:  wpkh([fingerprint/84h/coinTypeh/0h]xpub.../1/*)
///
/// The xpub encoded in the descriptor is the account-level extended public key
/// at path m/84'/{coinType}'/0', the same value stored in AccountInfo.ExtPubKey.
/// </summary>
public static class StandardWalletDescriptors
{
    private const int DefaultPurpose = 84;
    private const int DefaultAccountIndex = 0;

    /// <summary>
    /// Builds the receive (external, branch 0) and change (internal, branch 1)
    /// wpkh descriptor strings for the given account-level extended public key.
    /// </summary>
    /// <param name="accountExtPubKey">
    ///   Network-serialised account xpub (e.g. "tpubXXX…" for testnet).
    ///   This is the key at path m/84'/{coinType}'/{accountIndex}'.
    /// </param>
    /// <param name="masterFingerprint">
    ///   Optional 4-byte master key fingerprint as lower-case hex (e.g. "aabbccdd").
    ///   When present the descriptor origin "[fingerprint/84h/…]" is included.
    /// </param>
    /// <param name="coinType">BIP-44 coin type (0 = mainnet, 1 = testnet).</param>
    /// <param name="purpose">BIP-43 purpose field, defaults to 84 (BIP-84).</param>
    /// <param name="accountIndex">Account index, defaults to 0.</param>
    /// <returns>A tuple of (ReceiveDescriptor, ChangeDescriptor).</returns>
    public static (string Receive, string Change) Build(
        string accountExtPubKey,
        string? masterFingerprint,
        int coinType,
        int purpose = DefaultPurpose,
        int accountIndex = DefaultAccountIndex)
    {
        if (string.IsNullOrWhiteSpace(accountExtPubKey))
            throw new ArgumentException("Account extended public key cannot be empty.", nameof(accountExtPubKey));

        var origin = BuildOriginPart(masterFingerprint, purpose, coinType, accountIndex);
        var receive = $"wpkh({origin}{accountExtPubKey}/0/*)";
        var change  = $"wpkh({origin}{accountExtPubKey}/1/*)";
        return (receive, change);
    }

    /// <summary>
    /// Extracts the account-level xpub string from a wpkh descriptor.
    /// Works for both descriptors with and without an origin block.
    /// </summary>
    /// <exception cref="ArgumentException">If the descriptor is not a valid wpkh descriptor.</exception>
    public static string ExtractXPub(string descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor))
            throw new ArgumentException("Descriptor cannot be empty.", nameof(descriptor));

        if (!descriptor.StartsWith("wpkh(", StringComparison.Ordinal) || !descriptor.EndsWith(")"))
            throw new ArgumentException($"Not a valid wpkh descriptor: {descriptor}");

        // Strip leading "wpkh(" and trailing ")"
        var inner = descriptor[5..^1];

        // Strip optional origin block "[fingerprint/path]"
        if (inner.StartsWith('['))
        {
            var closeBracket = inner.IndexOf(']');
            if (closeBracket < 0)
                throw new ArgumentException($"Malformed descriptor origin in: {descriptor}");
            inner = inner[(closeBracket + 1)..];
        }

        // inner is now: xpub/branch/*
        // Find the trailing "/*"
        var wildcardIndex = inner.LastIndexOf("/*", StringComparison.Ordinal);
        if (wildcardIndex < 0)
            throw new ArgumentException($"Descriptor is missing the wildcard '/*': {descriptor}");

        // The branch digit sits immediately before "/*", preceded by a "/"
        var branchSlash = inner.LastIndexOf('/', wildcardIndex - 1);
        if (branchSlash < 0)
            throw new ArgumentException($"Descriptor is missing the branch segment: {descriptor}");

        return inner[..branchSlash];
    }

    /// <summary>
    /// Returns <c>true</c> when the descriptor represents the change (internal, branch 1) branch.
    /// </summary>
    public static bool IsChangeBranch(string descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor))
            return false;
        return descriptor.EndsWith("/1/*)", StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the <paramref name="accountInfo"/> has descriptor fields populated.
    /// If the account already has descriptors this is a no-op.
    /// If only the legacy <see cref="AccountInfo.ExtPubKey"/> is present the
    /// descriptors are generated from it so that existing wallets keep working.
    /// </summary>
    /// <param name="accountInfo">The account to migrate.</param>
    /// <param name="coinType">BIP-44 coin type for the current network.</param>
    /// <returns><c>true</c> if the descriptors were generated (migration occurred).</returns>
    public static bool TryMigrate(AccountInfo accountInfo, int coinType)
    {
        if (accountInfo is null)
            throw new ArgumentNullException(nameof(accountInfo));

        if (!string.IsNullOrEmpty(accountInfo.ReceiveDescriptor) &&
            !string.IsNullOrEmpty(accountInfo.ChangeDescriptor))
            return false; // Already descriptor-backed.

        if (string.IsNullOrEmpty(accountInfo.ExtPubKey))
            return false; // Cannot migrate without a legacy xpub.

        var (receive, change) = Build(
            accountInfo.ExtPubKey,
            accountInfo.MasterFingerprint,
            coinType);

        accountInfo.ReceiveDescriptor = receive;
        accountInfo.ChangeDescriptor  = change;
        return true;
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private static string BuildOriginPart(
        string? fingerprint, int purpose, int coinType, int accountIndex)
    {
        if (string.IsNullOrEmpty(fingerprint))
            return string.Empty;
        return $"[{fingerprint}/{purpose}h/{coinType}h/{accountIndex}h]";
    }
}
