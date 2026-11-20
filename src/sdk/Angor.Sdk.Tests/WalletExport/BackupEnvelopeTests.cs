using System.Security.Cryptography;
using Angor.Sdk.WalletExport;
using Angor.Sdk.WalletExport.Crypto;
using FluentAssertions;

namespace Angor.Sdk.Tests.WalletExport;

public class BackupEnvelopeTests
{
    private static BackupSeedPayload SamplePayload() => new()
    {
        WalletId = "8E3C5250-4E26-4A13-8075-0A189AEAF793",
        Network = "Main",
        Mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
        Bip39Passphrase = "",
        Label = "Daily wallet",
        CreatedAtUnix = 1_700_000_000
    };

    [Fact]
    public void Inner_roundtrip_recovers_payload()
    {
        using var keys = BackupKeys.FromPassphrase("strong-pass-123!");
        var payload = SamplePayload();

        var ct = BackupEnvelope.EncryptInner(payload, keys);
        var recovered = BackupEnvelope.DecryptInner(ct, keys);

        recovered.WalletId.Should().Be(payload.WalletId);
        recovered.Mnemonic.Should().Be(payload.Mnemonic);
        recovered.Network.Should().Be(payload.Network);
        recovered.Label.Should().Be(payload.Label);
    }

    [Fact]
    public void Inner_decrypt_with_wrong_passphrase_throws()
    {
        using var enc = BackupKeys.FromPassphrase("right-pass");
        using var dec = BackupKeys.FromPassphrase("wrong-pass");

        var ct = BackupEnvelope.EncryptInner(SamplePayload(), enc);

        Action act = () => BackupEnvelope.DecryptInner(ct, dec);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Inner_tampered_ciphertext_throws()
    {
        using var keys = BackupKeys.FromPassphrase("pass");
        var ct = BackupEnvelope.EncryptInner(SamplePayload(), keys);
        ct[^1] ^= 0x01;

        Action act = () => BackupEnvelope.DecryptInner(ct, keys);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Outer_roundtrip_recovers_manifest()
    {
        using var keys = BackupKeys.FromPassphrase("recovery-passphrase-xyz");
        var manifest = new BackupManifest
        {
            BlobSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            BlobSize = 512,
            Servers = new() { "https://blossom.angor.io", "https://nostr.build" },
            CreatedAtUnix = 1_700_000_000,
            Label = "Test"
        };

        var outer = BackupEnvelope.EncryptOuterManifest(manifest, keys);
        var recovered = BackupEnvelope.DecryptOuterManifest(outer, keys);

        recovered.BlobSha256.Should().Be(manifest.BlobSha256);
        recovered.BlobSize.Should().Be(manifest.BlobSize);
        recovered.Servers.Should().BeEquivalentTo(manifest.Servers);
        recovered.Algorithm.Should().Be(BackupManifest.CurrentAlgorithm);
    }

    [Fact]
    public void Outer_decrypt_with_wrong_passphrase_fails()
    {
        using var enc = BackupKeys.FromPassphrase("right");
        using var dec = BackupKeys.FromPassphrase("wrong");

        var manifest = new BackupManifest { BlobSha256 = "deadbeef", BlobSize = 1, Servers = new() { "x" } };
        var outer = BackupEnvelope.EncryptOuterManifest(manifest, enc);

        Action act = () => BackupEnvelope.DecryptOuterManifest(outer, dec);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Full_two_layer_roundtrip_with_unrelated_keys_fails()
    {
        using var keysAlice = BackupKeys.FromPassphrase("alice-pass");
        using var keysBob = BackupKeys.FromPassphrase("bob-pass");

        var manifest = new BackupManifest
        {
            BlobSha256 = "abc123",
            BlobSize = 32,
            Servers = new() { "https://blossom.angor.io" },
            CreatedAtUnix = 1_700_000_000
        };
        var aliceOuter = BackupEnvelope.EncryptOuterManifest(manifest, keysAlice);

        // Bob cannot read Alice's manifest
        Action act = () => BackupEnvelope.DecryptOuterManifest(aliceOuter, keysBob);
        act.Should().Throw<Exception>();
    }
}
