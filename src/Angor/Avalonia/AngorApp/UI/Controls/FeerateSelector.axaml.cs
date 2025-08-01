using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Controls;

public class FeerateSelector : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<IFeeratePreset>?> PresetsProperty = AvaloniaProperty.Register<FeerateSelector, IEnumerable<IFeeratePreset>?>(
        nameof(Presets));

    public IEnumerable<IFeeratePreset>? Presets
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
        this.WhenAnyValue(x => x.Amount).BindTo(this, x => x.Controller.Amount);
        this.WhenAnyValue(x => x.Presets).BindTo(this, x => x.Controller.UserProvided);
        this.WhenAnyValue(x => x.FeeCalculator).BindTo(this, x => x.Controller.FeeCalculator);
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

    public static readonly StyledProperty<IFeeCalculator> FeeCalculatorProperty = AvaloniaProperty.Register<FeerateSelector, IFeeCalculator>(
        nameof(FeeCalculator));

    public IFeeCalculator FeeCalculator
    {
        get => GetValue(FeeCalculatorProperty);
        set => SetValue(FeeCalculatorProperty, value);
    }

    public static readonly StyledProperty<long?> SatsProperty = AvaloniaProperty.Register<FeerateSelector, long?>(nameof(Amount), defaultBindingMode: BindingMode.OneWayToSource);

    public long? Amount
    {
        get => GetValue(SatsProperty);
        set => SetValue(SatsProperty, value);
    }
}

public partial class Controller : ReactiveValidationObject
{
    [Reactive] private IEnumerable<IFeeratePreset>? userProvided;
    [Reactive] private IFeerateViewModel? selectedFeeRate;
    [Reactive] private long? amount;
    [Reactive] IFeeCalculator? feeCalculator;
    [ObservableAsProperty] private IEnumerable<IFeerateViewModel> feeRates;
    [ObservableAsProperty] private long? feerate;

    public Controller()
    {
        this.ValidationRule(x => x.Feerate, d => d > 0 && d < 1000, "Invalid feerate");

        feeRatesHelper = this.WhenAnyValue(
                x => x.FeeCalculator,
                controller => controller.UserProvided,
                controller => controller.Amount, (calculator, presets, amount) => (calculator, presets, amount))
            .Select(a =>
            {
                var argCalculator = a.calculator;
                
                IEnumerable<IFeeratePreset> presets;
                if (a.presets != null)
                {
                    presets = a.presets.Select(preset => (IFeeratePreset)new Preset(preset.Name, preset.Feerate, amount.HasValue ? new AmountUI(amount.Value) : null, null));
                }
                else
                {
                    presets = [];
                }

                var argAmount = a.amount;
                var custom = new CustomFeeRate(argAmount, argCalculator);
                
                return presets.Append<IFeerateViewModel>(custom);
            }).ToProperty(this, controller => controller.FeeRates);

        var selectedFeeRates = this.WhenAnyValue<Controller, long?>(x => x.SelectedFeeRate!.Feerate.Sats).Where(l => l != 0);

        feerateHelper = selectedFeeRates.ToProperty(this, x => x.Feerate);
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
    [Reactive] private long sats = 10;
    [Reactive] private IAmountUI feerate;
    public IFeeCalculator? FeeCalculator { get; }
    [ObservableAsProperty] private IAmountUI fee;

    public CustomFeeRate(long? amount, IFeeCalculator? feeCalculator)
    {
        this.WhenAnyValue(rate => rate.Sats)
            .WhereNotNull()
            .Throttle(TimeSpan.FromSeconds(0.8), AvaloniaScheduler.Instance)
            .Select(l => new AmountUI(l))
            .BindTo(this, rate => rate.Feerate);
        
        if (feeCalculator != null && amount.HasValue)
        {
            FeeCalculator = feeCalculator;
            feeHelper = this.WhenAnyValue(x => x.Sats)
                .WhereNotNull()
                .Throttle(TimeSpan.FromSeconds(1), AvaloniaScheduler.Instance)
                .Select(sats => feeCalculator.GetFee(sats, amount.Value))
                .Switch()
                .Successes()
                .Select(l => new AmountUI(l))
                .ToProperty(this, x => x.Fee);
        }
    }
}

public partial class Preset : ReactiveObject, IFeeratePreset
{
    [ObservableAsProperty]
    private IAmountUI fee;

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
                .ToProperty(this, x => x.Fee);
            
            CalculateCommand.Execute().Subscribe();
        }
    }

    public ReactiveCommand<Unit, Result<long>> CalculateCommand { get; }

    public string Name { get; set; }

    public IAmountUI? Amount { get; }

    public IAmountUI Feerate { get; }
}

public interface IFeeratePreset : IFeerateViewModel
{
    public string Name { get; }
}