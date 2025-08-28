using System.Globalization;
using Angor.Contests.CrossCutting;
using Avalonia.Data.Converters;

namespace AngorApp.UI.Controls;

/// <summary>
/// Converts a Nostr npub (NIP-19 Bech32) public key (e.g. npub1...) into its 64-char hex form.
/// Returns empty string on failure. ConvertBack not supported.
/// </summary>
public sealed class NpubToHexConverter : IValueConverter
{
    public static NpubToHexConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return string.Empty;

        return NostrKeyCodec.TryNpubToHex(s.Trim(), out var hex) ? hex : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
