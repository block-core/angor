using Avalonia;

namespace AngorApp.UI.Shared.Controls;

public class SectionItem : ContentControl
{
    public static readonly StyledProperty<object> LeftContentProperty = AvaloniaProperty.Register<SectionItem, object>(
        nameof(LeftContent));

    public static readonly StyledProperty<object> RightContentProperty = AvaloniaProperty.Register<SectionItem, object>(
        nameof(RightContent));

    public object LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    public object RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }
}