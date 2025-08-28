using FluentAssertions;
using System.Text;
using Angor.Contests.CrossCutting;

namespace Angor.Contexts.Funding.Tests;

public class NostrKeyCodecTests
{
    private const string Bech32Chars = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

    [Fact]
    public void TryNpubToHex_ShouldReturnTrue_ForZeroBytesKey()
    {
        var hex = new string('0', 64);
        var npub = EncodeNpubFromHex(hex);
        var ok = NostrKeyCodec.TryNpubToHex(npub, out var decodedHex);
        ok.Should().BeTrue();
        decodedHex.Should().Be(hex);
    }

    [Fact]
    public void TryNpubToHex_ShouldReturnTrue_ForSampleHex()
    {
        var hex = "f75e8fb9ac1d1d2249b7a679536ef58397d00f77c221052b9360f418c024203c";
        var npub = EncodeNpubFromHex(hex);
        var ok = NostrKeyCodec.TryNpubToHex(npub, out var decodedHex);
        ok.Should().BeTrue();
        decodedHex.Should().Be(hex);
    }

    [Fact]
    public void TryNpubToHex_ShouldFail_ForNull()
    {
        var ok = NostrKeyCodec.TryNpubToHex(null, out var hex);
        ok.Should().BeFalse();
        hex.Should().BeEmpty();
    }

    [Fact]
    public void TryNpubToHex_ShouldFail_ForMixedCase()
    {
        var npub = EncodeNpubFromHex(new string('0', 64));
        var mixed = npub.Substring(0, 6) + npub.Substring(6).ToUpperInvariant();
        var ok = NostrKeyCodec.TryNpubToHex(mixed, out _);
        ok.Should().BeFalse();
    }

    [Fact]
    public void TryNpubToHex_ShouldFail_ForWrongHrp()
    {
        var npub = EncodeNpubFromHex(new string('0', 64));
        var wrong = npub.Replace("npub", "npux");
        var ok = NostrKeyCodec.TryNpubToHex(wrong, out _);
        ok.Should().BeFalse();
    }

    [Fact]
    public void TryNpubToHex_ShouldFail_ForCorruptedChecksum()
    {
        var npub = EncodeNpubFromHex(new string('0', 64));
        // Flip last char to another valid Bech32 char to break checksum
        var corrupted = npub[..^1] + (npub[^1] == 'q' ? 'p' : 'q');
        var ok = NostrKeyCodec.TryNpubToHex(corrupted, out _);
        ok.Should().BeFalse();
    }

    [Fact]
    public void ProvidedNpub_ShouldMatch_ExpectedHex()
    {
        var npub = "npub1efhgf25hf5q2lqz6w49nf0zw8jdgnx4vz3y85mewy8lfaf9elepshw5du2";
        var expectedHex = "ca6e84aa974d00af805a754b34bc4e3c9a899aac14487a6f2e21fe9ea4b9fe43";
        var ok = NostrKeyCodec.TryNpubToHex(npub, out var decoded);
        ok.Should().BeTrue();
        decoded.Should().Be(expectedHex);
    }

    // --- Helper encoder (minimal) to create valid npub test vectors ---
    private static string EncodeNpubFromHex(string hex)
    {
        var bytes = Enumerable.Range(0, hex.Length / 2)
            .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
        if (bytes.Length != 32) throw new ArgumentException("Expected 32 bytes");
        var fiveBitData = ConvertBits(bytes, 8, 5, true).Select(b => (int)b).ToList();
        var hrp = "npub";
        var checksum = CreateChecksum(hrp, fiveBitData.ToArray());
        var combined = fiveBitData.Concat(checksum).ToArray();
        var builder = new StringBuilder();
        builder.Append(hrp);
        builder.Append('1');
        foreach (var v in combined)
        {
            builder.Append(Bech32Chars[v]);
        }
        return builder.ToString();
    }

    private static int[] CreateChecksum(string hrp, int[] data)
    {
        var values = HrpExpand(hrp).Concat(data).Concat(Enumerable.Repeat(0, 6)).ToArray();
        var polymod = Polymod(values) ^ 1;
        var ret = new int[6];
        for (int i = 0; i < 6; i++)
        {
            ret[i] = (polymod >> 5 * (5 - i)) & 31;
        }
        return ret;
    }

    private static int[] HrpExpand(string hrp)
    {
        var ret = hrp.Select(c => c >> 5).ToList();
        ret.Add(0);
        ret.AddRange(hrp.Select(c => c & 31));
        return ret.ToArray();
    }

    private static int Polymod(int[] values)
    {
        int chk = 1;
        int[] generator = { 0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3 };
        foreach (var v in values)
        {
            int b = (chk >> 25) & 0xFF;
            chk = (chk & 0x1ffffff) << 5 ^ v;
            for (int i = 0; i < 5; i++)
            {
                if (((b >> i) & 1) != 0)
                {
                    chk ^= generator[i];
                }
            }
        }
        return chk;
    }

    private static byte[] ConvertBits(byte[] data, int fromBits, int toBits, bool pad)
    {
        int acc = 0;
        int bits = 0;
        int maxv = (1 << toBits) - 1;
        var ret = new System.Collections.Generic.List<byte>();
        foreach (var value in data)
        {
            if (value < 0 || (value >> fromBits) != 0) throw new ArgumentException("Invalid value");
            acc = (acc << fromBits) | value;
            bits += fromBits;
            while (bits >= toBits)
            {
                bits -= toBits;
                ret.Add((byte)((acc >> bits) & maxv));
            }
        }
        if (pad)
        {
            if (bits > 0)
            {
                ret.Add((byte)((acc << (toBits - bits)) & maxv));
            }
        }
        else if (bits >= fromBits || ((acc << (toBits - bits)) & maxv) != 0)
        {
            throw new ArgumentException("Invalid padding");
        }
        return ret.ToArray();
    }
}
