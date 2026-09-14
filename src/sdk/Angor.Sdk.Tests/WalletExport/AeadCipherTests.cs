using System.Security.Cryptography;
using System.Text;
using Angor.Sdk.WalletExport.Crypto;
using FluentAssertions;

namespace Angor.Sdk.Tests.WalletExport;

public class AeadCipherTests
{
    private static byte[] Key32() => Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    [Fact]
    public void Roundtrip_recovers_the_plaintext()
    {
        var key = Key32();
        var plaintext = Encoding.UTF8.GetBytes("the rain in spain falls mainly on the plain");

        var ct = AeadCipher.Encrypt(key, plaintext);
        var recovered = AeadCipher.Decrypt(key, ct);

        recovered.Should().Equal(plaintext);
    }

    [Fact]
    public void Wrong_key_throws()
    {
        var key = Key32();
        var wrong = Key32();
        wrong[0] ^= 0xFF;

        var ct = AeadCipher.Encrypt(key, Encoding.UTF8.GetBytes("secret"));

        Action act = () => AeadCipher.Decrypt(wrong, ct);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Tampered_tag_byte_throws()
    {
        var key = Key32();
        var ct = AeadCipher.Encrypt(key, Encoding.UTF8.GetBytes("secret"));

        // Tag sits at offset 12 (after the 12-byte nonce)
        ct[12] ^= 0x01;

        Action act = () => AeadCipher.Decrypt(key, ct);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Tampered_ciphertext_byte_throws()
    {
        var key = Key32();
        var ct = AeadCipher.Encrypt(key, Encoding.UTF8.GetBytes("secret bytes here"));

        // Ciphertext begins at offset 28 (12 nonce + 16 tag)
        ct[28] ^= 0x01;

        Action act = () => AeadCipher.Decrypt(key, ct);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Each_encryption_uses_a_fresh_nonce()
    {
        var key = Key32();
        var plaintext = Encoding.UTF8.GetBytes("same input each time");

        var a = AeadCipher.Encrypt(key, plaintext);
        var b = AeadCipher.Encrypt(key, plaintext);

        // Nonce + tag + ciphertext should differ entirely between calls
        a.Should().NotEqual(b);
        a.Take(12).Should().NotEqual(b.Take(12));
    }

    [Fact]
    public void Associated_data_mismatch_rejects()
    {
        var key = Key32();
        var aad1 = Encoding.UTF8.GetBytes("context-A");
        var aad2 = Encoding.UTF8.GetBytes("context-B");
        var ct = AeadCipher.Encrypt(key, Encoding.UTF8.GetBytes("payload"), aad1);

        Action act = () => AeadCipher.Decrypt(key, ct, aad2);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Buffer_shorter_than_overhead_rejected()
    {
        var key = Key32();
        Action act = () => AeadCipher.Decrypt(key, new byte[10]);
        act.Should().Throw<CryptographicException>();
    }
}
