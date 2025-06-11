using System.Collections;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;

namespace AngorApp.UI.Controls;

public class CardGrid : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable> ItemsSourceProperty =
        AvaloniaProperty.Register<CardGrid, IEnumerable>(nameof(ItemsSource));

    public IEnumerable ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly StyledProperty<IDataTemplate> ItemTemplateProperty = AvaloniaProperty.Register<CardGrid, IDataTemplate>(
        nameof(ItemTemplate));

    public IDataTemplate ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }
}