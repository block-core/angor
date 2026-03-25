using System.Globalization;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace App.UI.Shared.Controls;

/// <summary>
/// A Button with an optional Icon property and Spacing between icon and content.
/// Replaces Zafiro's EnhancedButton with identical API surface.
/// Template: Panel > Border#MainBorder > DockPanel > [IconPresenter + ContentPresenter]
/// </summary>
public class IconButton : Button
{
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<IconButton, object?>(nameof(Icon));

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<IconButton, double>(nameof(Spacing), 8);

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<IconButton, BoxShadows>(nameof(BoxShadow));

    /// <summary>Icon content displayed before the main content (typically an i:Icon).</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Gap between icon and content.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Box shadow on the main border.</summary>
    public BoxShadows BoxShadow
    {
        get => GetValue(BoxShadowProperty);
        set => SetValue(BoxShadowProperty, value);
    }
}

/// <summary>
/// Converts a double value to a Thickness with only the right component set.
/// Used to convert IconButton.Spacing to icon margin.
/// </summary>
public class DoubleToRightMarginConverter : IValueConverter
{
    public static readonly DoubleToRightMarginConverter Instance = new();
    
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var d = value is double v ? v : 0;
        return new Thickness(0, 0, d, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
