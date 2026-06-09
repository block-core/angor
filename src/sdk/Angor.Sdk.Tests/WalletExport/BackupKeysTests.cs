using Angor.Sdk.WalletExport.Crypto;
using FluentAssertions;

namespace Angor.Sdk.Tests.WalletExport;

public class BackupKeysTests
{
    [Fact]
    public void Same_passphrase_derives_same_key_triple()
    {
        using var a = BackupKeys.FromPassphrase("correct horse battery staple");
        using var b = BackupKeys.FromPassphrase("correct horse battery staple");

        a.BackupPrivateKeyHex.Should().Be(b.BackupPrivateKeyHex);
        a.BackupPublicKeyHex.Should().Be(b.BackupPublicKeyHex);
        a.InnerAeadKey.Should().Equal(b.InnerAeadKey);
    }

    [Fact]
    public void Different_passphrases_derive_different_keys()
    {
        using var a = BackupKeys.FromPassphrase("passphrase-one");
        using var b = BackupKeys.FromPassphrase("passphrase-two");

        a.BackupPrivateKeyHex.Should().NotBe(b.BackupPrivateKeyHex);
        a.BackupPublicKeyHex.Should().NotBe(b.BackupPublicKeyHex);
        a.InnerAeadKey.Should().NotEqual(b.InnerAeadKey);
    }

    [Fact]
    public void Backup_pubkey_is_32_byte_x_only()
    {
        using var keys = BackupKeys.FromPassphrase("some-passphrase-123");

        keys.BackupPublicKeyHex.Length.Should().Be(64);
        keys.BackupPublicKeyHex.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Backup_private_key_is_32_byte_hex()
    {
        using var keys = BackupKeys.FromPassphrase("some-passphrase-123");

        keys.BackupPrivateKeyHex.Length.Should().Be(64);
        keys.BackupPrivateKeyHex.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Inner_aead_key_is_32_bytes()
    {
        using var keys = BackupKeys.FromPassphrase("some-passphrase-123");
        keys.InnerAeadKey.Length.Should().Be(32);
    }

    [Fact]
    public void Backup_private_key_and_inner_aead_key_are_distinct()
    {
        using var keys = BackupKeys.FromPassphrase("some-passphrase-123");

        // HKDF domain separation must keep these two derived keys different
        keys.InnerAeadKey.Should().NotEqual(keys.BackupPrivateKey);
    }

    [Fact]
    public void Dispose_zeros_secrets()
    {
        var keys = BackupKeys.FromPassphrase("some-passphrase");
        var aeadKeyRef = keys.InnerAeadKey;
        var skRef = keys.BackupPrivateKey;
        var masterRef = keys.MasterSeed;

        keys.Dispose();

        aeadKeyRef.Should().OnlyContain(b => b == 0);
        skRef.Should().OnlyContain(b => b == 0);
        masterRef.Should().OnlyContain(b => b == 0);
    }
}
