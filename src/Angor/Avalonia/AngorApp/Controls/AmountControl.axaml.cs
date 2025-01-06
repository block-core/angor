using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Zafiro.Avalonia.MigrateToZafiro;
using Zafiro.Reactive;

namespace AngorApp.Controls;

[TemplatePart("PART_NumericUpDown", typeof(NumericUpDown), IsRequired = true)]
public class AmountControl : TemplatedControl, IModifiable
{
    public static readonly StyledProperty<bool> IsBitcoinProperty = AvaloniaProperty.Register<AmountControl, bool>(
        nameof(IsBitcoin), defaultBindingMode: BindingMode.TwoWay, enableDataValidation: true);

    public static readonly StyledProperty<decimal?> ValueProperty = AvaloniaProperty.Register<AmountControl, decimal?>(
        nameof(Value), enableDataValidation: true);

    public static readonly StyledProperty<ulong?> SatoshisProperty = AvaloniaProperty.Register<AmountControl, ulong?>(
        nameof(Satoshis), defaultBindingMode: BindingMode.TwoWay, enableDataValidation: true);

    public static readonly StyledProperty<decimal?> BitcoinProperty = AvaloniaProperty.Register<AmountControl, decimal?>(
        nameof(Bitcoin), defaultBindingMode: BindingMode.TwoWay, enableDataValidation: true);

    private string unit = null!;

    public static readonly DirectProperty<AmountControl, string> UnitProperty = AvaloniaProperty.RegisterDirect<AmountControl, string>(
        nameof(Unit), o => o.Unit, (o, v) => o.Unit = v);

    public string Unit
    {
        get => unit;
        private set => SetAndRaise(UnitProperty, ref unit, value);
    }

    private bool syncing;
    private NumericUpDown numericUpDown;
    private readonly ISubject<Unit> modifySubject = new Subject<Unit>();
    private readonly CompositeDisposable disposable = new();

    public AmountControl()
    {
        this.WhenAnyValue(x => x.IsBitcoin, x => x ? "BTC" : "sats").BindTo(this, x => x.Unit);
        this.WhenAnyValue(x => x.Value, x => x.IsBitcoin).Do(x => Sync(x.Item1, IsBitcoin)).Subscribe();
        this.WhenAnyValue(x => x.Bitcoin, x => x.IsBitcoin).Do(x => Sync(x.Item1, true)).Subscribe();
        this.WhenAnyValue(x => x.Satoshis, x => x.IsBitcoin).Do(x => Sync(x.Item1, false)).Subscribe();
    }

    public bool IsBitcoin
    {
        get => GetValue(IsBitcoinProperty);
        set => SetValue(IsBitcoinProperty, value);
    }

    public decimal? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public ulong? Satoshis
    {
        get => GetValue(SatoshisProperty);
        set => SetValue(SatoshisProperty, value);
    }

    public decimal? Bitcoin
    {
        get => GetValue(BitcoinProperty);
        set => SetValue(BitcoinProperty, value);
    }

    protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
    {
        if (property == BitcoinProperty || property == ValueProperty || property == SatoshisProperty)
        {
            DataValidationErrors.SetError(this, error);
        }

        base.UpdateDataValidation(property, state, error);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var upDown = e.NameScope.Find<NumericUpDown>("PART_NumericUpDown");

        Debug.Assert(upDown != null);

        numericUpDown = upDown;

        Observable.FromEventPattern<NumericUpDownValueChangedEventArgs>(handler => numericUpDown.ValueChanged += handler, handler => numericUpDown.ValueChanged -= handler)
            .ToSignal()
            .Subscribe(modifySubject)
            .DisposeWith(disposable);
    }

    private void Sync(decimal? d, bool isBtc)
    {
        if (syncing)
        {
            return;
        }

        syncing = true;

        if (isBtc)
        {
            Bitcoin = d;
            Satoshis = (ulong?)(d * 1_0000_0000);
        }
        else
        {
            Bitcoin = d / 1_0000_0000;
            Satoshis = (ulong?)d;
        }

        Value = IsBitcoin ? Bitcoin : Satoshis;

        syncing = false;
    }

    public IObservable<Unit> Modified => modifySubject.AsObservable();

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        disposable.Dispose();
        base.OnUnloaded(e);
    }
}