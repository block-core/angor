using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Styling;

namespace App.UI.Shared.Controls;

/// <summary>
/// A content control with Header, Subheader, HeaderStartContent, HeaderEndContent regions.
/// Replaces Zafiro's Card with identical API surface.
/// </summary>
public class Card : HeaderedContentControl
{
    public static readonly StyledProperty<object?> SubheaderProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(Subheader));

    public static readonly StyledProperty<object?> HeaderStartContentProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(HeaderStartContent));

    public static readonly StyledProperty<object?> HeaderEndContentProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(HeaderEndContent));

    public static readonly StyledProperty<IDataTemplate?> SubheaderTemplateProperty =
        AvaloniaProperty.Register<Card, IDataTemplate?>(nameof(SubheaderTemplate));

    public static readonly StyledProperty<IDataTemplate?> HeaderStartContentTemplateProperty =
        AvaloniaProperty.Register<Card, IDataTemplate?>(nameof(HeaderStartContentTemplate));

    public static readonly StyledProperty<IDataTemplate?> HeaderEndContentTemplateProperty =
        AvaloniaProperty.Register<Card, IDataTemplate?>(nameof(HeaderEndContentTemplate));

    public static readonly StyledProperty<ControlTheme?> HeaderThemeProperty =
        AvaloniaProperty.Register<Card, ControlTheme?>(nameof(HeaderTheme));

    public static readonly StyledProperty<ControlTheme?> SubheaderThemeProperty =
        AvaloniaProperty.Register<Card, ControlTheme?>(nameof(SubheaderTheme));

    public static readonly StyledProperty<ControlTheme?> ContentThemeProperty =
        AvaloniaProperty.Register<Card, ControlTheme?>(nameof(ContentTheme));

    public static readonly StyledProperty<double> HeaderAndBodySpacingProperty =
        AvaloniaProperty.Register<Card, double>(nameof(HeaderAndBodySpacing), 10);

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<Card, BoxShadows>(nameof(BoxShadow));

    public object? Subheader
    {
        get => GetValue(SubheaderProperty);
        set => SetValue(SubheaderProperty, value);
    }

    public object? HeaderStartContent
    {
        get => GetValue(HeaderStartContentProperty);
        set => SetValue(HeaderStartContentProperty, value);
    }

    public object? HeaderEndContent
    {
        get => GetValue(HeaderEndContentProperty);
        set => SetValue(HeaderEndContentProperty, value);
    }

    public IDataTemplate? SubheaderTemplate
    {
        get => GetValue(SubheaderTemplateProperty);
        set => SetValue(SubheaderTemplateProperty, value);
    }

    public IDataTemplate? HeaderStartContentTemplate
    {
        get => GetValue(HeaderStartContentTemplateProperty);
        set => SetValue(HeaderStartContentTemplateProperty, value);
    }

    public IDataTemplate? HeaderEndContentTemplate
    {
        get => GetValue(HeaderEndContentTemplateProperty);
        set => SetValue(HeaderEndContentTemplateProperty, value);
    }

    public ControlTheme? HeaderTheme
    {
        get => GetValue(HeaderThemeProperty);
        set => SetValue(HeaderThemeProperty, value);
    }

    public ControlTheme? SubheaderTheme
    {
        get => GetValue(SubheaderThemeProperty);
        set => SetValue(SubheaderThemeProperty, value);
    }

    public ControlTheme? ContentTheme
    {
        get => GetValue(ContentThemeProperty);
        set => SetValue(ContentThemeProperty, value);
    }

    public double HeaderAndBodySpacing
    {
        get => GetValue(HeaderAndBodySpacingProperty);
        set => SetValue(HeaderAndBodySpacingProperty, value);
    }

    public BoxShadows BoxShadow
    {
        get => GetValue(BoxShadowProperty);
        set => SetValue(BoxShadowProperty, value);
    }
}
