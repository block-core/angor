using Avalonia;
using Avalonia.Controls.Primitives;

namespace App.UI.Shared.Controls;

/// <summary>
/// A compact stat card showing a label and value, used in the Funds stats row
/// and potentially in the header bar. Vue: "stat-card" with title + value.
/// </summary>
public class StatCard : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StatCard, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<StatCard, string?>(nameof(Value));

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<StatCard, string?>(nameof(Unit));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }
}
