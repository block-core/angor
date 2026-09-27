using Angor.Sdk.WalletExport.Crypto;
using FluentAssertions;

namespace Angor.Sdk.Tests.WalletExport;

public class Argon2idKdfTests
{
    [Fact]
    public void Same_passphrase_always_derives_same_bytes()
    {
        var a = Argon2idKdf.Derive("correct horse battery staple");
        var b = Argon2idKdf.Derive("correct horse battery staple");

        a.Should().Equal(b);
        a.Length.Should().Be(32);
    }

    [Fact]
    public void Different_passphrase_derives_different_bytes()
    {
        var a = Argon2idKdf.Derive("correct horse battery staple");
        var b = Argon2idKdf.Derive("Correct horse battery staple"); // capital C

        a.Should().NotEqual(b);
    }

    [Fact]
    public void Empty_passphrase_throws()
    {
        Action act = () => Argon2idKdf.Derive("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Output_is_32_bytes()
    {
        var bytes = Argon2idKdf.Derive("anything goes here");
        bytes.Length.Should().Be(32);
    }

    [Fact]
    public void Unicode_normalisation_makes_equivalent_forms_match()
    {
        // U+00E9 (precomposed é) and U+0065+U+0301 (e + combining acute) must hash to the same value
        // because the KDF normalises to NFC.
        var precomposed = "café";
        var decomposed = "café";

        var a = Argon2idKdf.Derive(precomposed);
        var b = Argon2idKdf.Derive(decomposed);

        a.Should().Equal(b);
    }
}
