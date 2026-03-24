using Avalonia;
using Avalonia.Media;

namespace AngorApp.UI.Shared.Controls;

public class Badge : ContentControl
{
    public static readonly StyledProperty<Color> ColorProperty = AvaloniaProperty.Register<Badge, Color>(
        nameof(Color));

    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }
}