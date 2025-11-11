using Avalonia;
using Avalonia.Controls.Primitives;

namespace AngorApp.UI.Shared.Controls;

public class ErrorSummary : TemplatedControl
{
    public static readonly StyledProperty<ICollection<string>> ErrorsProperty = AvaloniaProperty.Register<ErrorSummary, ICollection<string>>(
        nameof(Errors));

    public ICollection<string> Errors
    {
        get => GetValue(ErrorsProperty);
        set => SetValue(ErrorsProperty, value);
    }
}