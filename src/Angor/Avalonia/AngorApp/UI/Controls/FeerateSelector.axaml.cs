using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
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
        this.WhenAnyValue(x => x.Controller.Feerate).Select(l => l).Subscribe(l => Feerate = l);
    }

    private long? feerate;

    public static readonly DirectProperty<FeerateSelector, long?> FeerateProperty = AvaloniaProperty.RegisterDirect<FeerateSelector, long?>(
        nameof(Feerate), o => o.Feerate, (o, v) => o.Feerate = v, defaultBindingMode: BindingMode.OneWayToSource);

    public long? Feerate
    {
        get => feerate;
        set => SetAndRaise(FeerateProperty, ref feerate, value);
    }
}

public partial class Controller : ReactiveValidationObject
{
    [Reactive] private long? customFeerate;
    [Reactive] private bool useCustomFeerate;
    [Reactive] private IEnumerable<Preset>? presets;
    [Reactive] private Preset? selectedPreset;
    [ObservableAsProperty] private long? feerate;

    public Controller()
    {
        this.ValidationRule(x => x.CustomFeerate, d => d > 0 && d < 1000, "Invalid custom feerate");
        this.ValidationRule(x => x.Feerate, d => d > 0 && d < 1000, "Invalid feerate");
        var feerates = this.WhenAnyValue(x => x.CustomFeerate, x => x.SelectedPreset, x => x.UseCustomFeerate, (custom, preset, useCustom) => useCustom ? custom : preset?.SatsPerVByte);
        feerateHelper = feerates.ToProperty(this, x => x.Feerate);
    }
}

public class Preset
{
    public string Name { get; set; }
    public long SatsPerVByte { get; set; }
}