using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

namespace App.Test.Integration.LayoutRegression;

/// <summary>
/// Guards checkbox visibility in both theme variants.
///
/// Root cause guarded here: the unchecked checkbox border was mapped to the
/// generic 'Stroke' token (10% white in dark / 10% black in light), which made
/// an unticked box effectively invisible on the Send modal's dark surface.
/// The unchecked state must paint clearly distinguishable border pixels.
/// </summary>
public class CheckBoxVisibilityTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Unchecked_checkbox_border_is_visible(bool dark)
    {
        var app = Application.Current!;
        var previousVariant = app.RequestedThemeVariant;
        app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;

        var checkBox = new CheckBox { Content = "Verify on-chain address", IsChecked = false };
        var window = new Window
        {
            Width = 400,
            Height = 200,
            SizeToContent = SizeToContent.Manual,
            Background = new SolidColorBrush(dark ? Color.Parse("#1A1A1A") : Colors.White),
            Content = new Border { Padding = new Thickness(24), Child = checkBox },
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var box = checkBox.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(b => b.Name == "NormalRectangle");
            box.Should().NotBeNull("the checkbox template must contain the NormalRectangle box");

            var borderBrush = box!.BorderBrush.Should().BeAssignableTo<ISolidColorBrush>().Subject;
            var bg = dark ? Color.Parse("#1A1A1A") : Colors.White;
            var contrast = Contrast(Composite(borderBrush.Color, bg), bg);
            contrast.Should().BeGreaterThan(1.6,
                $"the unchecked checkbox border ({borderBrush.Color}) must be clearly visible " +
                $"against a {(dark ? "dark" : "light")} surface ({bg}) — a near-transparent " +
                "'Stroke' token renders the unticked state invisible");
        }
        finally
        {
            window.Close();
            app.RequestedThemeVariant = previousVariant;
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Alpha-composite a (possibly translucent) color over a background.</summary>
    private static Color Composite(Color fg, Color bg)
    {
        var a = fg.A / 255.0;
        return Color.FromRgb(
            (byte)(fg.R * a + bg.R * (1 - a)),
            (byte)(fg.G * a + bg.G * (1 - a)),
            (byte)(fg.B * a + bg.B * (1 - a)));
    }

    /// <summary>WCAG contrast ratio between two opaque colors.</summary>
    private static double Contrast(Color c1, Color c2)
    {
        var l1 = Luminance(c1);
        var l2 = Luminance(c2);
        var (hi, lo) = l1 >= l2 ? (l1, l2) : (l2, l1);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance(Color c)
    {
        static double Chan(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Chan(c.R) + 0.7152 * Chan(c.G) + 0.0722 * Chan(c.B);
    }
}
