using Avalonia;
using Avalonia.Controls;

namespace AngorApp.UI.Shared.Controls;

/// <summary>
/// A ContentControl that wraps its content in a ScrollViewer with configurable padding.
/// Use this as the root element inside UserControls to avoid repeating the ScrollViewer + padding pattern.
/// </summary>
public class ScrollableView : ContentControl
{
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<ScrollableView, Thickness>(nameof(ContentPadding), new Thickness(20));

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
}
