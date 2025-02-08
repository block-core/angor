using System.Collections;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;

namespace AngorApp.UI.Controls;

public class List : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable> ItemsSourceProperty = AvaloniaProperty.Register<List, IEnumerable>(
        nameof(ItemsSource));
    
    [Content]
    public IEnumerable ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly StyledProperty<object> HeaderProperty = AvaloniaProperty.Register<List, object>(
        nameof(Header));

    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }
}