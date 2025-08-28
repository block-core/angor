namespace Angor.Contests.CrossCutting;

// Minimal NIP-19 (npub) codec. Validates Bech32 checksum and converts both ways.
public static class NostrKeyCodec
{
    private const string Bech32Chars = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

    // --------- Decode: npub -> hex --------------------------------------------------

    public static bool TryNpubToHex(string? npub, out string hex)
    {
        hex = string.Empty;
        if (string.IsNullOrWhiteSpace(npub)) return false;
        try
        {
            var result = DecodeNpubToHex(npub.Trim());
            if (result == null) return false;
            hex = result;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string NpubToHex(string npub) =>
        DecodeNpubToHex(npub) ?? throw new FormatException("Invalid npub");

    public static string? DecodeNpubToHex(string npub)
    {
        if (!npub.StartsWith("npub", StringComparison.OrdinalIgnoreCase)) return null;

        // Bech32 forbids mixed case.
        var hasUpper = npub.Any(char.IsUpper);
        var hasLower = npub.Any(char.IsLower);
        if (hasUpper && hasLower) return null;

        npub = npub.ToLowerInvariant();
        var pos = npub.LastIndexOf('1');
        if (pos < 1 || pos + 7 > npub.Length) return null; // hrp + sep + data (>=6 checksum)

        var hrp = npub[..pos];
        if (hrp != "npub") return null;

        var dataPart = npub[(pos + 1)..];
        if (dataPart.Any(c => !Bech32Chars.Contains(c))) return null;

        var dataValues = dataPart.Select(c => Bech32Chars.IndexOf(c)).ToArray();
        if (!VerifyChecksum(hrp, dataValues)) return null;

        var payload = dataValues.Take(dataValues.Length - 6).ToArray();
        var bytes = ConvertBits(payload, 5, 8, pad: false);
        if (bytes == null || bytes.Length != 32) return null;

        return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }

    // --------- Encode: hex (64) -> npub ---------------------------------------------

    public static bool TryHexToNpub(string? hex, out string npub)
    {
        npub = string.Empty;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        try
        {
            var result = EncodeHexToNpub(hex.Trim());
            if (result == null) return false;
            npub = result;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string HexToNpub(string hex) =>
        EncodeHexToNpub(hex) ?? throw new FormatException("Invalid 64-char hex public key");

    public static string? EncodeHexToNpub(string? hex)
    {
        // Accept upper/lowercase; reject 0x prefix and mixed non-hex.
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return null;
        if (hex.Length != 64) return null;
        if (!IsStrictHex(hex)) return null;

        var bytes = HexToBytes(hex);
        if (bytes == null || bytes.Length != 32) return null;

        // Convert 8-bit bytes to 5-bit groups with padding, as per Bech32.
        var fiveBit = ConvertBits(bytes.Select(b => (int)b).ToArray(), 8, 5, pad: true);
        if (fiveBit == null || fiveBit.Length == 0) return null;

        var payload = fiveBit.Select(b => (int)b).ToArray();

        // Build checksum and final data.
        var checksum = CreateChecksum("npub", payload);
        var combined = payload.Concat(checksum).ToArray();

        // Map to charset.
        var dataPart = new string(combined.Select(i => Bech32Chars[i]).ToArray());

        return "npub" + "1" + dataPart; // Always lowercase Bech32.
    }

    // --------- Bech32 internals ------------------------------------------------------

    private static int[] HrpExpand(string hrp)
    {
        var ret = hrp.Select(c => c >> 5).ToList();
        ret.Add(0);
        ret.AddRange(hrp.Select(c => c & 31));
        return ret.ToArray();
    }

    private static bool VerifyChecksum(string hrp, int[] data) =>
        Polymod(HrpExpand(hrp).Concat(data).ToArray()) == 1;

    // Create 6 checksum values such that polymod(expand(hrp) || data || checksum) == 1.
    private static int[] CreateChecksum(string hrp, int[] data)
    {
        var values = HrpExpand(hrp).Concat(data).Concat(new int[6]).ToArray();
        var pm = Polymod(values) ^ 1;

        // Extract 6 groups of 5 bits, MSB group first.
        var checksum = new int[6];
        for (int i = 0; i < 6; i++)
        {
            checksum[i] = (pm >> (5 * (5 - i))) & 31;
        }
        return checksum;
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

    // Converts between bit widths. When padding, leftover bits are zero-padded on the right.
    private static byte[]? ConvertBits(int[] data, int fromBits, int toBits, bool pad)
    {
        int acc = 0;
        int bits = 0;
        int maxv = (1 << toBits) - 1;
        var ret = new System.Collections.Generic.List<byte>();
        foreach (var value in data)
        {
            if (value < 0) return null;
            if ((value >> fromBits) != 0) return null;
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
            return null;
        }
        return ret.ToArray();
    }

    // --------- Hex helpers -----------------------------------------------------------

    // Strict hex validation: exactly [0-9a-fA-F].
    private static bool IsStrictHex(string s)
    {
        foreach (var ch in s)
        {
            bool isHex =
                (ch >= '0' && ch <= '9') ||
                (ch >= 'a' && ch <= 'f') ||
                (ch >= 'A' && ch <= 'F');
            if (!isHex) return false;
        }
        return true;
    }

    private static byte[]? HexToBytes(string hex)
    {
        try
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
        catch
        {
            return null;
        }
    }
}
