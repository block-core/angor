using Avalonia;

namespace AngorApp.UI.Controls;

public class IconButton : Button
{
    public static readonly StyledProperty<object> IconProperty = AvaloniaProperty.Register<IconButton, object>(nameof(Icon));
    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}