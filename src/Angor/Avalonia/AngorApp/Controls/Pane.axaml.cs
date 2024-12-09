using Avalonia;
using Avalonia.Styling;

namespace AngorApp.Controls;

public class Pane : ContentControl
{
    public static readonly StyledProperty<object> TitleProperty = AvaloniaProperty.Register<Pane, object>(
        nameof(Title));

    public object Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<object> IconProperty = AvaloniaProperty.Register<Pane, object>(
        nameof(Icon));

    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly StyledProperty<Uri> HeaderIconProperty = AvaloniaProperty.Register<Pane, Uri>(
        nameof(HeaderIcon));

    public Uri HeaderIcon
    {
        get => GetValue(HeaderIconProperty);
        set => SetValue(HeaderIconProperty, value);
    }

    public static readonly StyledProperty<Uri> HeaderBackgroundProperty = AvaloniaProperty.Register<Pane, Uri>(
        nameof(HeaderBackground));

    public Uri HeaderBackground
    {
        get => GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }
    
    public static readonly StyledProperty<object> TitleIconProperty = AvaloniaProperty.Register<Pane, object>(
        nameof(TitleIcon));

    public object TitleIcon
    {
        get => GetValue(TitleIconProperty);
        set => SetValue(TitleIconProperty, value);
    }

    public static readonly StyledProperty<bool> IsHeaderVisibleProperty = AvaloniaProperty.Register<Pane, bool>(
        nameof(IsHeaderVisible));

    public bool IsHeaderVisible
    {
        get => GetValue(IsHeaderVisibleProperty);
        set => SetValue(IsHeaderVisibleProperty, value);
    }

    public static readonly StyledProperty<bool> IsTitleVisibleProperty = AvaloniaProperty.Register<Pane, bool>(
        nameof(IsTitleVisible));

    public bool IsTitleVisible
    {
        get => GetValue(IsTitleVisibleProperty);
        set => SetValue(IsTitleVisibleProperty, value);
    }

    public static readonly StyledProperty<Thickness> TitlePaddingProperty = AvaloniaProperty.Register<Pane, Thickness>(
        nameof(TitlePadding));

    public Thickness TitlePadding
    {
        get => GetValue(TitlePaddingProperty);
        set => SetValue(TitlePaddingProperty, value);
    }

    public static readonly StyledProperty<object> TitleRightContentProperty = AvaloniaProperty.Register<Pane, object>(
        nameof(TitleRightContent));

    public object TitleRightContent
    {
        get => GetValue(TitleRightContentProperty);
        set => SetValue(TitleRightContentProperty, value);
    }
}