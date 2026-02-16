using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace AngorApp.UI.Shared.Controls;

public class NewCard : ContentControl
{
    public static readonly StyledProperty<string> ContainerClassesProperty = AvaloniaProperty.Register<NewCard, string>(
        nameof(ContainerClasses));

    public string ContainerClasses
    {
        get => GetValue(ContainerClassesProperty);
        set => SetValue(ContainerClassesProperty, value);
    }

    public static readonly StyledProperty<string> HeaderPanelClassesProperty = AvaloniaProperty.Register<NewCard, string>(
        nameof(HeaderPanelClasses), "Icon Orange Size-S");

    public string HeaderPanelClasses
    {
        get => GetValue(HeaderPanelClassesProperty);
        set => SetValue(HeaderPanelClassesProperty, value);
    }

    public static readonly StyledProperty<object> LeftProperty = AvaloniaProperty.Register<NewCard, object>(
        nameof(Left));

    public object Left
    {
        get => GetValue(LeftProperty);
        set => SetValue(LeftProperty, value);
    }

    public static readonly StyledProperty<string> LeftClassesProperty = AvaloniaProperty.Register<NewCard, string>(
        nameof(LeftClasses));

    public string LeftClasses
    {
        get => GetValue(LeftClassesProperty);
        set => SetValue(LeftClassesProperty, value);
    }

    public static readonly StyledProperty<object> CenterProperty = AvaloniaProperty.Register<NewCard, object>(
        nameof(Center));

    public object Center
    {
        get => GetValue(CenterProperty);
        set => SetValue(CenterProperty, value);
    }

    public static readonly StyledProperty<string> CenterClassesProperty = AvaloniaProperty.Register<NewCard, string>(
        nameof(CenterClasses));

    public string CenterClasses
    {
        get => GetValue(CenterClassesProperty);
        set => SetValue(CenterClassesProperty, value);
    }

    public static readonly StyledProperty<object> RightProperty = AvaloniaProperty.Register<NewCard, object>(
        nameof(Right));

    public object Right
    {
        get => GetValue(RightProperty);
        set => SetValue(RightProperty, value);
    }

    public static readonly StyledProperty<string> RightClassesProperty = AvaloniaProperty.Register<NewCard, string>(
        nameof(RightClasses));

    public string RightClasses
    {
        get => GetValue(RightClassesProperty);
        set => SetValue(RightClassesProperty, value);
    }

    public static readonly StyledProperty<string> ContentClassesProperty = AvaloniaProperty.Register<NewCard, string>(
        nameof(ContentClasses));

    public string ContentClasses
    {
        get => GetValue(ContentClassesProperty);
        set => SetValue(ContentClassesProperty, value);
    }
}
