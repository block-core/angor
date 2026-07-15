using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace App.UI.Shared.Helpers;

/// <summary>
/// Strips markdown syntax down to readable plain text for clamped one-to-three line
/// summaries (project cards, hero blurbs) where full markdown rendering isn't wanted
/// but raw markers (**, #, [](), `) would look broken.
/// Use <see cref="Instance"/> in XAML: Text="{Binding Description, Converter={x:Static helpers:MarkdownToPlainConverter.Instance}}"
/// </summary>
public sealed partial class MarkdownToPlainConverter : IValueConverter
{
    public static readonly MarkdownToPlainConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s ? StripToPlain(s) : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"<a\b[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"^#{1,6}\s+", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\s*(?:[-*+]|\d+\.)\s+", RegexOptions.Multiline)]
    private static partial Regex ListMarkerRegex();

    /// <summary>Convert markdown to plain text: drops emphasis/code markers, heading
    /// hashes, list markers, blockquote arrows, code fences; links become their label.</summary>
    public static string StripToPlain(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return markdown;

        var text = markdown.Replace("\r\n", "\n");
        text = text.Replace("```", "");
        text = ImageRegex().Replace(text, "");        // drop images entirely
        text = LinkRegex().Replace(text, "$1");
        text = AnchorRegex().Replace(text, "$1");     // <a>label</a> → label
        text = HtmlTagRegex().Replace(text, " ");     // drop remaining tags (<img>, <video>, <br>…)
        text = HeadingRegex().Replace(text, "");
        text = ListMarkerRegex().Replace(text, "");

        var sb = new StringBuilder(text.Length);
        foreach (var line in text.Split('\n'))
        {
            var cleaned = line.TrimStart('>', ' ').Trim();
            if (cleaned.Length == 0) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(cleaned);
        }

        // Emphasis / inline-code markers
        return sb.ToString().Replace("**", "").Replace("__", "").Replace("`", "");
    }
}
