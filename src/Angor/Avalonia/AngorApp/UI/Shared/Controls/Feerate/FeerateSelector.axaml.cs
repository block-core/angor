using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Shared.Controls.Feerate;

public class FeerateSelector : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<IFeeratePreset>?> PresetsProperty = AvaloniaProperty.Register<FeerateSelector, IEnumerable<IFeeratePreset>?>(
        nameof(Presets));

    public static readonly StyledProperty<Controller> ControllerProperty = AvaloniaProperty.Register<FeerateSelector, Controller>(
        nameof(Controller));

    public static readonly DirectProperty<FeerateSelector, long?> FeerateProperty = AvaloniaProperty.RegisterDirect<FeerateSelector, long?>(
        nameof(Feerate), o => o.Feerate, (o, v) => o.Feerate = v, defaultBindingMode: BindingMode.OneWayToSource);

    public static readonly StyledProperty<IFeeCalculator?> FeeCalculatorProperty = AvaloniaProperty.Register<FeerateSelector, IFeeCalculator?>(
        nameof(FeeCalculator));

    public static readonly StyledProperty<long?> AmountProperty = AvaloniaProperty.Register<FeerateSelector, long?>(nameof(Amount), defaultBindingMode: BindingMode.OneWayToSource);

    private long? feerate;

    public FeerateSelector()
    {
        Controller = new Controller();
        this.WhenAnyValue(x => x.Amount).BindTo(this, x => x.Controller.Amount);
        this.WhenAnyValue(x => x.Presets).BindTo(this, x => x.Controller.UserProvided);
        this.WhenAnyValue(x => x.FeeCalculator).BindTo(this, x => x.Controller.FeeCalculator);
        this.WhenAnyValue(x => x.Controller.Feerate).Select(l => l).Subscribe(l => Feerate = l);
    }

    public IEnumerable<IFeeratePreset>? Presets
    {
        get => GetValue(PresetsProperty);
        set => SetValue(PresetsProperty, value);
    }

    public Controller Controller
    {
        get => GetValue(ControllerProperty);
        set => SetValue(ControllerProperty, value);
    }

    public long? Feerate
    {
        get => feerate;
        set => SetAndRaise(FeerateProperty, ref feerate, value);
    }

    public IFeeCalculator? FeeCalculator
    {
        get => GetValue(FeeCalculatorProperty);
        set => SetValue(FeeCalculatorProperty, value);
    }

    public long? Amount
    {
        get => GetValue(AmountProperty);
        set => SetValue(AmountProperty, value);
    }
}

public interface IFeeCalculator
{
    Task<Result<long>> GetFee(long feerate, long amount);
}

public interface IFeerateViewModel
{
    IAmountUI Feerate { get; }
}

public partial class CustomFeeRate : ReactiveObject, IFeerateViewModel
{
    [ObservableAsProperty] private IAmountUI fee;
    [Reactive] private IAmountUI feerate;
    [Reactive] private long sats = 10;

    public CustomFeeRate(long? amount, IFeeCalculator? feeCalculator)
    {
        this.WhenAnyValue<CustomFeeRate, long>(rate => rate.Sats)
            .WhereNotNull()
            .Throttle(TimeSpan.FromSeconds(0.8), AvaloniaScheduler.Instance)
            .Select(l => new AmountUI(l))
            .BindTo<AmountUI, CustomFeeRate, IAmountUI>(this, rate => rate.Feerate);

        if (feeCalculator != null && amount.HasValue)
        {
            FeeCalculator = feeCalculator;
            feeHelper = this.WhenAnyValue<CustomFeeRate, long>(x => x.Sats)
                .WhereNotNull()
                .Throttle(TimeSpan.FromSeconds(1), AvaloniaScheduler.Instance)
                .Select(sats => feeCalculator.GetFee(sats, amount.Value))
                .Switch()
                .Successes()
                .Select(l => new AmountUI(l))
                .ToProperty<CustomFeeRate, IAmountUI>(this, x => x.Fee);
        }
    }

    public IFeeCalculator? FeeCalculator { get; }
}

public partial class Preset : ReactiveObject, IFeeratePreset
{
    [ObservableAsProperty] private IAmountUI fee;

    public Preset(string name, IAmountUI feeRate, IAmountUI? amount, IFeeCalculator? feeCalculator)
    {
        Name = name;
        Amount = amount;
        Feerate = feeRate;
        if (feeCalculator != null && amount != null)
        {
            CalculateCommand = ReactiveCommand.CreateFromTask(() => feeCalculator.GetFee(feeRate.Sats, amount.Sats));

            feeHelper = CalculateCommand.Successes()
                .Select(sats => new AmountUI(sats))
                .ToProperty<Preset, IAmountUI>(this, x => x.Fee);

            CalculateCommand.Execute().Subscribe();
        }
    }

    public ReactiveCommand<Unit, Result<long>> CalculateCommand { get; }

    public IAmountUI? Amount { get; }

    public string Name { get; set; }

    public IAmountUI Feerate { get; }
}

public interface IFeeratePreset : IFeerateViewModel
{
    public string Name { get; }
}