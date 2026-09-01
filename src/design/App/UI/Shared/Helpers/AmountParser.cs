using System.Globalization;

namespace App.UI.Shared.Helpers;

/// <summary>
/// Culture-safe parsing of amount strings.
///
/// Rules (see PR #956):
/// - Strings the app formats itself (InvariantCulture, dot decimal) must be parsed
///   back with InvariantCulture — never the OS culture. On comma-decimal locales
///   (de-DE, fr-FR, ...) current-culture parsing of "0.05000000" fails or misparses,
///   which caused the founder Claim button to silently do nothing.
/// - User-typed input must tolerate both ',' and '.' as the decimal separator.
/// </summary>
public static class AmountParser
{
    /// <summary>
    /// Parses a user-typed amount, accepting both ',' and '.' as decimal separator.
    /// Never throws; returns false for null/blank/invalid input.
    /// </summary>
    public static bool TryParseUserAmount(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <inheritdoc cref="TryParseUserAmount(string?, out double)"/>
    public static bool TryParseUserAmount(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Parses an app-formatted (InvariantCulture) amount string; returns 0 when invalid.
    /// Use for round-trip strings the app formatted itself, NOT for user input.
    /// </summary>
    public static double ParseInvariantOrZero(string? amount)
    {
        return double.TryParse(amount, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
