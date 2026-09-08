using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using App.UI.Shared;
using App.UI.Shared.Controls;
using Xunit;

namespace App.Test.Integration.LayoutRegression;

/// <summary>
/// WalletCard content regression tests.
///
/// Root cause guarded here: the global <c>FontFamilySans</c> token referenced a
/// bare family name ("Inter", previously "Roboto"). Bare names are resolved
/// against the SYSTEM font collection — on Android neither bundled font is a
/// system font, so every TextBlock inheriting the token rendered BLANK (the
/// Send/Receive buttons showed empty chrome). Embedded fonts registered via
/// WithInterFont() must be referenced by URI: <c>fonts:Inter#Inter</c>.
///
/// Guards:
/// 1. The FontFamilySans token must resolve to a real glyph typeface in the
///    font manager (fails for bare names that aren't installed system fonts).
/// 2. WalletCard's Send/Receive buttons must contain laid-out text AND paint
///    non-background pixels at desktop and compact widths.
/// </summary>
public class WalletCardContentTests
{
    [AvaloniaFact]
    public void FontFamilySans_token_resolves_to_a_glyph_typeface()
    {
        var app = Application.Current!;
        app.TryGetResource("FontFamilySans", app.ActualThemeVariant, out var resource)
            .Should().BeTrue("the FontFamilySans token must exist");

        var fontFamily = resource.Should().BeOfType<FontFamily>().Subject;

        FontManager.Current.TryGetGlyphTypeface(
                new Typeface(fontFamily, FontStyle.Normal, FontWeight.Normal),
                out var glyphTypeface)
            .Should().BeTrue(
                $"FontFamilySans ('{fontFamily}') must resolve to an actual glyph typeface — " +
                "bare family names silently fall back to system fonts and render blank " +
                "on platforms that don't ship them (e.g. Android). Use the embedded " +
                "URI form, e.g. 'fonts:Inter#Inter'.");

        // The resolved face must be the requested family, not a silent fallback.
        glyphTypeface!.FamilyName.Should().Be(fontFamily.FamilyNames.PrimaryFamilyName);
    }

    [AvaloniaTheory]
    [InlineData(1280d, true)]  // desktop layout, dark theme (device default)
    [InlineData(1280d, false)] // desktop layout, light theme
    [InlineData(360d, true)]   // compact layout, dark theme
    [InlineData(360d, false)]  // compact layout, light theme
    public void WalletCard_send_and_receive_buttons_render_visible_text(double width, bool dark)
    {
        var app = Application.Current!;
        var previousVariant = app.RequestedThemeVariant;
        app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        LayoutModeService.Instance.UpdateWidth(width);

        var card = new WalletCard
        {
            WalletName = "Angor Wallet",
            Balance = "0.00000000 BTC",
            WalletType = "On-Chain",
            WalletId = "test-wallet",
        };

        var window = new Window
        {
            Width = width,
            Height = 720,
            SizeToContent = SizeToContent.Manual,
            Content = card,
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            AssertButtonRendersText(window, card, "BtnSend", width);
            AssertButtonRendersText(window, card, "BtnReceive", width);
        }
        finally
        {
            window.Close();
            app.RequestedThemeVariant = previousVariant;
            LayoutModeService.Instance.UpdateWidth(1280);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void AssertButtonRendersText(Window window, WalletCard card, string buttonName, double width)
    {
        var button = card.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Name == buttonName);
        button.Should().NotBeNull($"{buttonName} must exist in the WalletCard template at width {width}");

        var text = button!.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
        text.Should().NotBeNull($"{buttonName} must contain its label TextBlock at width {width}");
        text!.Bounds.Width.Should().BeGreaterThan(1,
            $"{buttonName} label '{text.Text}' must have laid-out width at {width} — " +
            "zero width means the font produced no glyphs");
        text.Bounds.Height.Should().BeGreaterThan(1,
            $"{buttonName} label must have laid-out height at {width}");

        // Pixel-level guard: the label area must contain non-background pixels.
        // Catches blank text even when layout metrics come from a fallback shaper.
        using var frame = window.CaptureRenderedFrame();
        frame.Should().NotBeNull();

        var topLeft = text.TranslatePoint(new Point(0, 0), window);
        topLeft.Should().NotBeNull();

        using var bmp = frame!;
        var pixels = CountDistinctColors(bmp, topLeft!.Value, text.Bounds.Size);
        pixels.Should().BeGreaterThan(1,
            $"{buttonName} label region must paint text pixels distinct from the button " +
            $"background at width {width} — a single flat color means the text is invisible");
    }

    private static int CountDistinctColors(global::Avalonia.Media.Imaging.WriteableBitmap bmp, Point origin, Size size)
    {
        using var fb = bmp.Lock();
        var scale = fb.Size.Width / (double)bmp.Size.Width;
        var startX = (int)(origin.X * scale);
        var startY = (int)(origin.Y * scale);
        var w = (int)(size.Width * scale);
        var h = (int)(size.Height * scale);

        var colors = new HashSet<int>();
        for (var y = startY; y < startY + h && y < fb.Size.Height; y++)
        {
            var rowBase = fb.Address + y * fb.RowBytes;
            for (var x = startX; x < startX + w && x < fb.Size.Width; x++)
            {
                colors.Add(System.Runtime.InteropServices.Marshal.ReadInt32(rowBase, x * 4));
                if (colors.Count > 1) return colors.Count;
            }
        }
        return colors.Count;
    }
}
