using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace App.UI.Shared.Controls;

/// <summary>
/// A panel with Start, Content (center), and End regions arranged horizontally.
/// Replaces Zafiro's EdgePanel with identical API surface.
/// Default template: DockPanel with Start docked left, End docked right, Content fills center.
/// </summary>
public class EdgePanel : TemplatedControl
{
    public static readonly StyledProperty<object?> StartContentProperty =
        AvaloniaProperty.Register<EdgePanel, object?>(nameof(StartContent));

    public static readonly StyledProperty<object?> ContentProperty =
        ContentControl.ContentProperty.AddOwner<EdgePanel>();

    public static readonly StyledProperty<object?> EndContentProperty =
        AvaloniaProperty.Register<EdgePanel, object?>(nameof(EndContent));

    public static readonly StyledProperty<IDataTemplate?> StartContentTemplateProperty =
        AvaloniaProperty.Register<EdgePanel, IDataTemplate?>(nameof(StartContentTemplate));

    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        ContentControl.ContentTemplateProperty.AddOwner<EdgePanel>();

    public static readonly StyledProperty<IDataTemplate?> EndContentTemplateProperty =
        AvaloniaProperty.Register<EdgePanel, IDataTemplate?>(nameof(EndContentTemplate));

    public static readonly StyledProperty<string?> StartContentClassesProperty =
        AvaloniaProperty.Register<EdgePanel, string?>(nameof(StartContentClasses));

    public static readonly StyledProperty<string?> ContentClassesProperty =
        AvaloniaProperty.Register<EdgePanel, string?>(nameof(ContentClasses));

    public static readonly StyledProperty<string?> EndContentClassesProperty =
        AvaloniaProperty.Register<EdgePanel, string?>(nameof(EndContentClasses));

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<EdgePanel, double>(nameof(Spacing), 0);

    public object? StartContent
    {
        get => GetValue(StartContentProperty);
        set => SetValue(StartContentProperty, value);
    }

    [Content]
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public object? EndContent
    {
        get => GetValue(EndContentProperty);
        set => SetValue(EndContentProperty, value);
    }

    public IDataTemplate? StartContentTemplate
    {
        get => GetValue(StartContentTemplateProperty);
        set => SetValue(StartContentTemplateProperty, value);
    }

    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    public IDataTemplate? EndContentTemplate
    {
        get => GetValue(EndContentTemplateProperty);
        set => SetValue(EndContentTemplateProperty, value);
    }

    public string? StartContentClasses
    {
        get => GetValue(StartContentClassesProperty);
        set => SetValue(StartContentClassesProperty, value);
    }

    public string? ContentClasses
    {
        get => GetValue(ContentClassesProperty);
        set => SetValue(ContentClassesProperty, value);
    }

    public string? EndContentClasses
    {
        get => GetValue(EndContentClassesProperty);
        set => SetValue(EndContentClassesProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }
}
