using System.Text;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using App.UI.Shared.Helpers;

namespace App.UI.Shared.Controls;

/// <summary>
/// Lightweight markdown renderer for project descriptions.
///
/// No external dependency (design decision: keep the dep tree minimal and match the
/// app's theme tokens exactly). Supports the subset produced by typical project
/// profiles: headings (#..####), paragraphs, bold/italic, inline code, fenced code
/// blocks, links, unordered/ordered lists, blockquotes and horizontal rules.
/// Unknown syntax degrades gracefully to plain text.
///
/// Usage: &lt;controls:MarkdownTextBlock Markdown="{Binding DisplayDescription}" /&gt;
/// </summary>
public class MarkdownTextBlock : StackPanel
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownTextBlock, string?>(nameof(Markdown));

    /// <summary>Base font size for body text; headings scale from this.</summary>
    public static readonly StyledProperty<double> BaseFontSizeProperty =
        AvaloniaProperty.Register<MarkdownTextBlock, double>(nameof(BaseFontSize), 15);

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public double BaseFontSize
    {
        get => GetValue(BaseFontSizeProperty);
        set => SetValue(BaseFontSizeProperty, value);
    }

    static MarkdownTextBlock()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownTextBlock>((c, _) => c.Rebuild());
        BaseFontSizeProperty.Changed.AddClassHandler<MarkdownTextBlock>((c, _) => c.Rebuild());
    }

    public MarkdownTextBlock()
    {
        Spacing = 10;
        Orientation = Orientation.Vertical;
    }

    // ── Theme brushes (resolved lazily with sane fallbacks) ──

    private IBrush BodyBrush => FindBrush("TextMutedBrush", Brushes.Gray);
    private IBrush StrongBrush => FindBrush("TextStrongBrush", Brushes.Black);
    private IBrush LinkBrush => FindBrush("Brand", new SolidColorBrush(Color.Parse("#F7931A")));
    private IBrush CodeBackground => FindBrush("SurfaceMedium", new SolidColorBrush(Color.Parse("#22808080")));
    private IBrush QuoteBar => FindBrush("Brand", new SolidColorBrush(Color.Parse("#F7931A")));

    private IBrush FindBrush(string key, IBrush fallback)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var value))
        {
            if (value is IBrush brush) return brush;
            if (value is Color color) return new SolidColorBrush(color);
        }

        return fallback;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild(); // resources are resolvable now
    }

    // ── Block-level parsing ──

    private void Rebuild()
    {
        Children.Clear();

        var text = Markdown;
        if (string.IsNullOrWhiteSpace(text)) return;

        // Normalize inline HTML that project profiles commonly contain:
        // <a href="url">label</a> → [label](url), <br> → newline.
        text = MarkdownMedia.AnchorRegex().Replace(text, m => $"[{m.Groups[2].Value.Trim()}]({m.Groups[1].Value})");
        text = MarkdownMedia.BreakRegex().Replace(text, "\n");

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var paragraph = new List<string>();
        int i = 0;

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            var content = string.Join(" ", paragraph).Trim();
            paragraph.Clear();
            if (content.Length > 0)
                AddParagraphWithMedia(content);
        }

        while (i < lines.Length)
        {
            var raw = lines[i];
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();

            // Blank line: paragraph boundary
            if (trimmed.Length == 0)
            {
                FlushParagraph();
                i++;
                continue;
            }

            // Fenced code block
            if (trimmed.StartsWith("```"))
            {
                FlushParagraph();
                var code = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```"))
                {
                    code.AppendLine(lines[i]);
                    i++;
                }

                i++; // skip closing fence
                Children.Add(CreateCodeBlock(code.ToString().TrimEnd('\n')));
                continue;
            }

            // Heading
            if (trimmed.StartsWith('#'))
            {
                int level = 0;
                while (level < trimmed.Length && trimmed[level] == '#' && level < 6) level++;
                if (level < trimmed.Length && trimmed[level] == ' ')
                {
                    FlushParagraph();
                    var headingText = trimmed[(level + 1)..].Trim().TrimEnd('#').TrimEnd();
                    var size = BaseFontSize * level switch
                    {
                        1 => 1.6,
                        2 => 1.35,
                        3 => 1.15,
                        _ => 1.05
                    };
                    var heading = CreateTextBlock(headingText, size, StrongBrush, FontWeight.Bold);
                    heading.Margin = new Thickness(0, Children.Count == 0 ? 0 : 6, 0, 0);
                    Children.Add(heading);
                    i++;
                    continue;
                }
            }

            // Horizontal rule
            if (trimmed is "---" or "***" or "___")
            {
                FlushParagraph();
                Children.Add(new Border
                {
                    Height = 1,
                    Background = CodeBackground,
                    Margin = new Thickness(0, 4)
                });
                i++;
                continue;
            }

            // Blockquote
            if (trimmed.StartsWith('>'))
            {
                FlushParagraph();
                var quote = new List<string>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith('>'))
                {
                    quote.Add(lines[i].TrimStart().TrimStart('>').TrimStart());
                    i++;
                }

                Children.Add(new Border
                {
                    BorderBrush = QuoteBar,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(12, 2, 0, 2),
                    Child = CreateTextBlock(string.Join(" ", quote), BaseFontSize, BodyBrush, FontWeight.Normal,
                        italic: true, lineHeight: BaseFontSize * 1.5)
                });
                continue;
            }

            // Unordered / ordered list item
            if (IsUnorderedItem(trimmed, out var bulletText) || IsOrderedItem(trimmed, out bulletText, out _))
            {
                FlushParagraph();
                var list = new StackPanel { Spacing = 4 };
                int ordinal = 1;
                while (i < lines.Length)
                {
                    var itemLine = lines[i].TrimStart();
                    string? marker = null;
                    if (IsUnorderedItem(itemLine, out var itemText)) marker = "\u2022";
                    else if (IsOrderedItem(itemLine, out itemText, out var number)) marker = number + ".";
                    if (marker == null) break;
                    i++;

                    // Lazy continuation: hard-wrapped item text continues on following
                    // lines until a blank line, a new list marker, or another block start.
                    while (i < lines.Length)
                    {
                        var next = lines[i].TrimStart();
                        var nextTrimmed = next.TrimEnd();
                        if (nextTrimmed.Length == 0) break;
                        if (IsUnorderedItem(next, out _) || IsOrderedItem(next, out _, out _)) break;
                        if (next.StartsWith('#') || next.StartsWith('>') || next.StartsWith("```")) break;
                        itemText += " " + nextTrimmed;
                        i++;
                    }

                    var lineHeight = BaseFontSize * 1.5;
                    var row = new DockPanel { HorizontalSpacing = 8 };
                    var markerBlock = CreateTextBlock(marker == "\u2022" ? marker : $"{ordinal}.", BaseFontSize, BodyBrush, FontWeight.SemiBold,
                        lineHeight: lineHeight);
                    markerBlock.MinWidth = 18;
                    DockPanel.SetDock(markerBlock, Dock.Left);
                    markerBlock.VerticalAlignment = VerticalAlignment.Top;
                    row.Children.Add(markerBlock);
                    row.Children.Add(CreateTextBlock(itemText, BaseFontSize, BodyBrush, FontWeight.Normal, lineHeight: lineHeight));
                    list.Children.Add(row);
                    ordinal++;
                }

                Children.Add(list);
                continue;
            }

            paragraph.Add(trimmed);
            i++;
        }

        FlushParagraph();
    }

    private static bool IsUnorderedItem(string line, out string text)
    {
        if (line.Length > 2 && (line[0] is '-' or '*' or '+') && line[1] == ' ')
        {
            text = line[2..].Trim();
            return true;
        }

        text = "";
        return false;
    }

    private static bool IsOrderedItem(string line, out string text, out string number)
    {
        int d = 0;
        while (d < line.Length && char.IsDigit(line[d])) d++;
        if (d > 0 && d + 1 < line.Length && line[d] == '.' && line[d + 1] == ' ')
        {
            number = line[..d];
            text = line[(d + 2)..].Trim();
            return true;
        }

        text = "";
        number = "";
        return false;
    }

    // ── Media: markdown images, inline HTML <img>/<video> ──

    /// <summary>
    /// Emit a paragraph, splitting out media tokens (markdown images and HTML img/video
    /// tags) into real controls. Consecutive images flow into a WrapPanel row.
    /// </summary>
    private void AddParagraphWithMedia(string content)
    {
        var matches = MarkdownMedia.MediaRegex().Matches(content);
        if (matches.Count == 0)
        {
            Children.Add(CreateTextBlock(content, BaseFontSize, BodyBrush, FontWeight.Normal, lineHeight: BaseFontSize * 1.55));
            return;
        }

        int cursor = 0;
        WrapPanel? imageRow = null;

        void FlushText(string segment)
        {
            var s = segment.Trim();
            if (s.Length > 0)
            {
                imageRow = null;
                Children.Add(CreateTextBlock(s, BaseFontSize, BodyBrush, FontWeight.Normal, lineHeight: BaseFontSize * 1.55));
            }
        }

        void AddMedia(Control control, bool isImage)
        {
            if (isImage)
            {
                if (imageRow == null)
                {
                    imageRow = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
                    Children.Add(imageRow);
                }

                imageRow.Children.Add(control);
            }
            else
            {
                imageRow = null;
                Children.Add(control);
            }
        }

        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            FlushText(content[cursor..m.Index]);
            cursor = m.Index + m.Length;

            if (m.Groups["mdalt"].Success || m.Groups["mdurl"].Success)
            {
                AddMedia(CreateImage(m.Groups["mdurl"].Value, null, null), isImage: true);
            }
            else if (m.Value.StartsWith("<img", StringComparison.OrdinalIgnoreCase))
            {
                var src = MarkdownMedia.Attr(m.Value, "src");
                if (!string.IsNullOrEmpty(src))
                    AddMedia(CreateImage(src, MarkdownMedia.AttrInt(m.Value, "width"), MarkdownMedia.AttrInt(m.Value, "height")), isImage: true);
            }
            else // <video>
            {
                var src = MarkdownMedia.Attr(m.Value, "src");
                if (!string.IsNullOrEmpty(src))
                    AddMedia(CreateVideoCard(src), isImage: false);
            }
        }

        FlushText(content[cursor..]);
    }

    private Control CreateImage(string url, int? width, int? height)
    {
        // Cap layout size; decode downscaled so oversized logos never become giant textures.
        var maxWidth = Math.Min(width ?? 400, 640);

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly
        };
        if (height is > 0) image.MaxHeight = height.Value;

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            MaxWidth = maxWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = image
        };

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            ImageCacheService.LoadBitmapAsync(url, bmp => image.Source = bmp, decodeWidth: maxWidth * 2);
        }

        return border;
    }

    /// <summary>Inline video playback isn't supported — render a themed card that opens
    /// the video in the default browser/player.</summary>
    private Control CreateVideoCard(string url)
    {
        var card = new Border
        {
            Background = CodeBackground,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "\u25B6", FontSize = BaseFontSize, Foreground = LinkBrush, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock
                    {
                        Text = "Watch video",
                        FontSize = BaseFontSize,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = LinkBrush,
                        TextDecorations = TextDecorations.Underline,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            card.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Left)
                    ExplorerHelper.OpenUrl(url);
            };
        }

        return card;
    }

    private Control CreateCodeBlock(string code)
    {
        return new Border
        {
            Background = CodeBackground,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8),
            Child = new SelectableTextBlock
            {
                Text = code,
                FontFamily = new FontFamily("Menlo,Consolas,Courier New,monospace"),
                FontSize = BaseFontSize - 1,
                Foreground = StrongBrush,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    // ── Inline parsing: **bold**, *italic*, _italic_, `code`, [text](url) ──

    private TextBlock CreateTextBlock(string content, double fontSize, IBrush brush, FontWeight weight,
        bool italic = false, double? lineHeight = null)
    {
        var block = new TextBlock
        {
            FontSize = fontSize,
            Foreground = brush,
            FontWeight = weight,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
            TextWrapping = TextWrapping.Wrap
        };
        if (lineHeight is > 0) block.LineHeight = lineHeight.Value;

        foreach (var inline in ParseInlines(content, brush, fontSize))
            block.Inlines?.Add(inline);

        return block;
    }

    private IEnumerable<Inline> ParseInlines(string text, IBrush baseBrush, double fontSize)
    {
        var inlines = new List<Inline>();
        int pos = 0;
        var plain = new StringBuilder();

        void FlushPlain()
        {
            if (plain.Length == 0) return;
            inlines.Add(new Run(plain.ToString()));
            plain.Clear();
        }

        while (pos < text.Length)
        {
            // Link: [text](url)
            if (text[pos] == '[')
            {
                int closeBracket = text.IndexOf(']', pos);
                if (closeBracket > pos && closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
                {
                    int closeParen = text.IndexOf(')', closeBracket + 2);
                    if (closeParen > 0)
                    {
                        FlushPlain();
                        var label = text[(pos + 1)..closeBracket];
                        var url = text[(closeBracket + 2)..closeParen];
                        inlines.Add(CreateLink(label, url, fontSize));
                        pos = closeParen + 1;
                        continue;
                    }
                }
            }

            // Bold: **text**
            if (Matches(text, pos, "**"))
            {
                int end = text.IndexOf("**", pos + 2, StringComparison.Ordinal);
                if (end > pos)
                {
                    FlushPlain();
                    inlines.Add(new Run(text[(pos + 2)..end]) { FontWeight = FontWeight.Bold, Foreground = StrongBrush });
                    pos = end + 2;
                    continue;
                }
            }

            // Italic: *text* or _text_
            if ((text[pos] == '*' || text[pos] == '_') && pos + 1 < text.Length && text[pos + 1] != ' ')
            {
                char marker = text[pos];
                int end = text.IndexOf(marker, pos + 1);
                if (end > pos + 1)
                {
                    FlushPlain();
                    inlines.Add(new Run(text[(pos + 1)..end]) { FontStyle = FontStyle.Italic });
                    pos = end + 1;
                    continue;
                }
            }

            // Inline code: `code`
            if (text[pos] == '`')
            {
                int end = text.IndexOf('`', pos + 1);
                if (end > pos)
                {
                    FlushPlain();
                    inlines.Add(new Run(text[(pos + 1)..end])
                    {
                        FontFamily = new FontFamily("Menlo,Consolas,Courier New,monospace"),
                        FontSize = fontSize - 1,
                        Foreground = StrongBrush,
                        Background = CodeBackground
                    });
                    pos = end + 1;
                    continue;
                }
            }

            plain.Append(text[pos]);
            pos++;
        }

        FlushPlain();
        return inlines;
    }

    private Inline CreateLink(string label, string url, double fontSize)
    {
        // Runs can't receive pointer events, so links are hosted in an InlineUIContainer.
        var linkText = new TextBlock
        {
            Text = label,
            FontSize = fontSize,
            Foreground = LinkBrush,
            TextDecorations = TextDecorations.Underline,
            Cursor = new Cursor(StandardCursorType.Hand),
            TextWrapping = TextWrapping.Wrap
        };

        // Only open http(s) links — nostr:/magnet:/etc are ignored rather than shelled out.
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            linkText.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Left)
                    ExplorerHelper.OpenUrl(url);
            };
        }

        return new InlineUIContainer(linkText) { BaselineAlignment = BaselineAlignment.TextBottom };
    }

    private static bool Matches(string text, int pos, string token) =>
        pos + token.Length <= text.Length && text.AsSpan(pos, token.Length).SequenceEqual(token);
}

/// <summary>Shared regexes/attribute helpers for media embedded in project descriptions.</summary>
internal static partial class MarkdownMedia
{
    /// <summary>Markdown image ![alt](url), HTML &lt;img …&gt; and &lt;video …&gt;(&lt;/video&gt;).</summary>
    [System.Text.RegularExpressions.GeneratedRegex(
        @"!\[(?<mdalt>[^\]]*)\]\((?<mdurl>[^)\s]+)\)|<img\b[^>]*/?>|<video\b[^>]*>(?:\s*</video>)?",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    public static partial System.Text.RegularExpressions.Regex MediaRegex();

    /// <summary>HTML anchor: &lt;a href="url" …&gt;label&lt;/a&gt;.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(
        @"<a\b[^>]*href=[""'](?<url>[^""']+)[""'][^>]*>(?<label>.*?)</a>",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline)]
    private static partial System.Text.RegularExpressions.Regex AnchorRegexImpl();

    [System.Text.RegularExpressions.GeneratedRegex(@"<br\s*/?>", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    public static partial System.Text.RegularExpressions.Regex BreakRegex();

    public static System.Text.RegularExpressions.Regex AnchorRegex() => AnchorRegexImpl();

    /// <summary>Extract an attribute value from an HTML tag string.</summary>
    public static string? Attr(string tag, string name)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            tag, name + @"\s*=\s*[""']([^""']*)[""']",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    public static int? AttrInt(string tag, string name)
    {
        var value = Attr(tag, name);
        if (value == null || value.Contains('%')) return null; // percentage sizes → let layout decide
        return int.TryParse(value, out var n) && n > 0 ? n : null;
    }
}
