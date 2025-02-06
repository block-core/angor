using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Metadata;

namespace AngorApp.UI.Controls;

public class IconLabel : TemplatedControl
{
    public static readonly StyledProperty<string> IconProperty = AvaloniaProperty.Register<IconLabel, string>(
        nameof(Icon));

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly StyledProperty<IBrush> IconBrushProperty = AvaloniaProperty.Register<IconLabel, IBrush>(
        nameof(IconBrush));

    public IBrush IconBrush
    {
        get => GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<IconLabel, string>(
        nameof(Text));

    [Content]
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}