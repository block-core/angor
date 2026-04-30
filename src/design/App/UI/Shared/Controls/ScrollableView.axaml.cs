using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace App.UI.Shared.Controls;

/// <summary>
/// A ContentControl that wraps its content in a ScrollViewer with configurable padding.
/// Use this as the root element inside UserControls to avoid repeating the ScrollViewer + padding pattern.
/// </summary>
public class ScrollableView : ContentControl
{
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<ScrollableView, Thickness>(nameof(ContentPadding), new Thickness(24));

    public static readonly StyledProperty<double> MaxContentWidthProperty =
        AvaloniaProperty.Register<ScrollableView, double>(nameof(MaxContentWidth), double.PositiveInfinity);

    /// <summary>
    /// Gets or sets the padding applied to the content inside the ScrollViewer.
    /// Default is 20 on all sides.
    /// </summary>
    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum width of the content.
    /// Default is infinity (no limit).
    /// </summary>
    public double MaxContentWidth
    {
        get => GetValue(MaxContentWidthProperty);
        set => SetValue(MaxContentWidthProperty, value);
    }

    /// <summary>
    /// Mobile (Android/iOS): hide the vertical scrollbar. Touch users flick to
    /// scroll and the scrollbar track is visual noise that overlaps the right
    /// edge of cards. Desktop keeps the default auto-visible scrollbar because
    /// mouse users rely on it as a position indicator and drag target.
    /// </summary>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            // Template root is the ScrollViewer — find it via the visual tree
            // (GetTemplateChildren isn't exposed on ContentControl in this Avalonia version).
            var sv = this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (sv != null)
            {
                sv.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }
        }
    }
}
