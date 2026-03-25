using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace App.UI.Shared.Controls;

/// <summary>
/// A content control with a Header region above the Content.
/// Replaces Zafiro's HeaderedContainer with identical API surface.
/// Supports HeaderPadding, ContentPadding, HeaderBackground, ContentClasses,
/// HeaderTemplate, ContentTemplate, and BoxShadow.
/// </summary>
public class HeaderedContainer : HeaderedContentControl
{
    public static readonly StyledProperty<Thickness> HeaderPaddingProperty =
        AvaloniaProperty.Register<HeaderedContainer, Thickness>(nameof(HeaderPadding));

    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<HeaderedContainer, Thickness>(nameof(ContentPadding));

    public static readonly StyledProperty<IBrush?> HeaderBackgroundProperty =
        AvaloniaProperty.Register<HeaderedContainer, IBrush?>(nameof(HeaderBackground));

    public static readonly StyledProperty<string?> ContentClassesProperty =
        AvaloniaProperty.Register<HeaderedContainer, string?>(nameof(ContentClasses));

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<HeaderedContainer, BoxShadows>(nameof(BoxShadow));

    public Thickness HeaderPadding
    {
        get => GetValue(HeaderPaddingProperty);
        set => SetValue(HeaderPaddingProperty, value);
    }

    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public IBrush? HeaderBackground
    {
        get => GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    public string? ContentClasses
    {
        get => GetValue(ContentClassesProperty);
        set => SetValue(ContentClassesProperty, value);
    }

    public BoxShadows BoxShadow
    {
        get => GetValue(BoxShadowProperty);
        set => SetValue(BoxShadowProperty, value);
    }
}
