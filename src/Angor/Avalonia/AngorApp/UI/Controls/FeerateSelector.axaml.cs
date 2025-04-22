using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Controls;

public class FeerateSelector : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<Preset>?> PresetsProperty = AvaloniaProperty.Register<FeerateSelector, IEnumerable<Preset>?>(
        nameof(Presets));

    public IEnumerable<Preset>? Presets
    {
        get => GetValue(PresetsProperty);
        set => SetValue(PresetsProperty, value);
    }

    public static readonly StyledProperty<double> CustomFeerateProperty = AvaloniaProperty.Register<FeerateSelector, double>(
        nameof(CustomFeerateProperty));

    public double CustomFeerate
    {
        get => GetValue(CustomFeerateProperty);
        set => SetValue(CustomFeerateProperty, value);
    }
    
    public static readonly StyledProperty<Controller> ControllerProperty = AvaloniaProperty.Register<FeerateSelector, Controller>(
        nameof(Controller));

    public Controller Controller
    {
        get => GetValue(ControllerProperty);
        set => SetValue(ControllerProperty, value);
    }

    public FeerateSelector()
    {
        Controller = new Controller();
        this.WhenAnyValue(x => x.Presets).BindTo(this, x => x.Controller.Presets);
    }
}

public partial class Controller : ReactiveValidationObject
{
    [Reactive] private double customFeerate;
    [Reactive] private bool useCustomFeerate;
    [Reactive] private IEnumerable<Preset>? presets;

    public Controller()
    {
        this.ValidationRule(x => x.CustomFeerate, d => d > 0 && d < 1000, "Invalid feerate");
    }
}

public class Preset
{
    public string Name { get; set; }
    public long SatsPerVByte { get; set; }
}