using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace AngorApp.UI.Controls;

public static class Automation
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static readonly FuncValueConverter<object, string?> ContentToAutomationId =
        new(o => o?.ToString().RemoveWhitespaceAndCapitalize());

    private static string RemoveWhitespaceAndCapitalize(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var withoutWhitespace = WhitespaceRegex.Replace(input.Trim(), " ");
        var textInfo = CultureInfo.InvariantCulture.TextInfo;
        var titleCased = textInfo.ToTitleCase(withoutWhitespace.ToLowerInvariant());
        return titleCased.Replace(" ", "");
    }
}